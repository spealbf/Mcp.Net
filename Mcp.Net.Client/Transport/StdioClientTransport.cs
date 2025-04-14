using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Mcp.Net.Core.Interfaces;
using Mcp.Net.Core.JsonRpc;
using Mcp.Net.Core.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mcp.Net.Client.Transport;

/// <summary>
/// Client transport implementation that uses standard input/output streams.
/// </summary>
public class StdioClientTransport : ClientMessageTransportBase
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<object>> _pendingRequests =
        new();
    private readonly Stream _inputStream;
    private readonly Stream _outputStream;
    private readonly Process? _serverProcess;
    private Task? _readTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="StdioClientTransport"/> class using default stdin/stdout.
    /// </summary>
    /// <param name="logger">Optional logger.</param>
    public StdioClientTransport(ILogger? logger = null)
        : base(new JsonRpcMessageParser(), logger ?? NullLogger.Instance)
    {
        _inputStream = Console.OpenStandardInput();
        _outputStream = Console.OpenStandardOutput();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StdioClientTransport"/> class.
    /// </summary>
    /// <param name="input">Input stream.</param>
    /// <param name="output">Output stream.</param>
    /// <param name="logger">Optional logger.</param>
    public StdioClientTransport(Stream input, Stream output, ILogger? logger = null)
        : base(new JsonRpcMessageParser(), logger ?? NullLogger.Instance)
    {
        _inputStream = input ?? throw new ArgumentNullException(nameof(input));
        _outputStream = output ?? throw new ArgumentNullException(nameof(output));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StdioClientTransport"/> class with a server command.
    /// </summary>
    /// <param name="serverCommand">The command to launch the server.</param>
    /// <param name="logger">Optional logger.</param>
    public StdioClientTransport(string serverCommand, ILogger? logger = null)
        : base(new JsonRpcMessageParser(), logger ?? NullLogger.Instance)
    {
        Logger.LogInformation("Launching server: {ServerCommand}", serverCommand);

        _serverProcess = new Process();
        _serverProcess.StartInfo = new ProcessStartInfo
        {
            FileName = "bash",
            Arguments = $"-c \"{serverCommand}\"",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        _serverProcess.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Logger.LogInformation("[SERVER] {ErrorData}", e.Data);
            }
        };

        _serverProcess.Start();
        _serverProcess.BeginErrorReadLine();

        _inputStream = _serverProcess.StandardOutput.BaseStream;
        _outputStream = _serverProcess.StandardInput.BaseStream;
    }

    /// <inheritdoc />
    public override Task StartAsync()
    {
        if (IsStarted)
        {
            throw new InvalidOperationException("Transport already started");
        }

        IsStarted = true;
        _readTask = ProcessMessagesAsync();
        Logger.LogDebug("StdioClientTransport started");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async Task<object> SendRequestAsync(string method, object? parameters = null)
    {
        if (IsClosed)
        {
            throw new InvalidOperationException("Transport is closed");
        }

        // Create a unique ID for this request
        var id = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<object>();
        _pendingRequests[id] = tcs;

        // Create the request message
        var request = new JsonRpcRequestMessage("2.0", id, method, parameters);

        try
        {
            // Send the request
            await WriteMessageAsync(request);

            // Wait for the response with a timeout
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(60), CancellationTokenSource.Token);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _pendingRequests.TryRemove(id, out _);
                throw new TimeoutException($"Request timed out: {method}");
            }

            return await tcs.Task;
        }
        catch (OperationCanceledException)
        {
            _pendingRequests.TryRemove(id, out _);
            throw;
        }
        catch (Exception ex)
        {
            _pendingRequests.TryRemove(id, out _);
            Logger.LogError(ex, "Error sending request: {Method}", method);
            throw;
        }
    }

    /// <inheritdoc />
    public override async Task SendNotificationAsync(string method, object? parameters = null)
    {
        if (IsClosed)
        {
            throw new InvalidOperationException("Transport is closed");
        }

        // Create the notification message
        var notification = new JsonRpcNotificationMessage(
            "2.0",
            method,
            parameters != null ? JsonSerializer.SerializeToElement(parameters) : null
        );

        // Send the notification
        await WriteMessageAsync(notification);
    }

    private async Task ProcessMessagesAsync()
    {
        var buffer = new byte[4096];
        var messageBuffer = new StringBuilder();

        try
        {
            while (!CancellationTokenSource.Token.IsCancellationRequested)
            {
                var bytesRead = await _inputStream.ReadAsync(
                    buffer,
                    0,
                    buffer.Length,
                    CancellationTokenSource.Token
                );

                if (bytesRead == 0)
                {
                    // End of stream
                    Logger.LogInformation("End of input stream detected");
                    break;
                }

                var chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                messageBuffer.Append(chunk);

                // Process buffer and find complete messages
                string content = messageBuffer.ToString();
                int newlineIndex;

                while ((newlineIndex = content.IndexOf('\n')) >= 0)
                {
                    string line = content.Substring(0, newlineIndex).Trim();
                    content = content.Substring(newlineIndex + 1);

                    if (!string.IsNullOrEmpty(line))
                    {
                        // Process the JSON-RPC message
                        if (MessageParser.IsJsonRpcResponse(line))
                        {
                            var response = MessageParser.DeserializeResponse(line);

                            // Find and complete the pending request
                            if (_pendingRequests.TryRemove(response.Id, out var tcs))
                            {
                                if (response.Error != null)
                                {
                                    Logger.LogError(
                                        "Request {Id} failed: {ErrorMessage}",
                                        response.Id,
                                        response.Error.Message
                                    );
                                    tcs.SetException(
                                        new Exception($"RPC Error: {response.Error.Message}")
                                    );
                                }
                                else
                                {
                                    Logger.LogDebug("Request {Id} succeeded", response.Id);
                                    tcs.SetResult(response.Result ?? new { });
                                }
                            }
                            else
                            {
                                Logger.LogWarning(
                                    "Received response for unknown request: {Id}",
                                    response.Id
                                );
                            }

                            // Also process in the base class for events
                            ProcessResponse(response);
                        }
                        else
                        {
                            Logger.LogWarning("Received unexpected message: {Line}", line);
                        }
                    }
                }

                messageBuffer.Clear();
                messageBuffer.Append(content);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
            Logger.LogDebug("Message processing cancelled");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error reading from input stream");
            RaiseOnError(ex);
        }
        finally
        {
            Logger.LogInformation("Message processing loop terminated");
        }
    }

    /// <inheritdoc />
    protected override async Task WriteRawAsync(byte[] data)
    {
        await _outputStream.WriteAsync(data, 0, data.Length, CancellationTokenSource.Token);
        await _outputStream.FlushAsync(CancellationTokenSource.Token);
    }

    /// <inheritdoc />
    protected override async Task OnClosingAsync()
    {
        // Terminate the server process if we started it
        if (_serverProcess != null && !_serverProcess.HasExited)
        {
            Logger.LogInformation("Terminating server process");
            try
            {
                _serverProcess.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error terminating server process");
            }
        }

        await base.OnClosingAsync();
    }
}
