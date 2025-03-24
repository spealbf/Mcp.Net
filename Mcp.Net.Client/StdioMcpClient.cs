using System.Text;
using System.Text.Json;
using System.Diagnostics;
using Mcp.Net.Client.Interfaces;
using Mcp.Net.Core;
using Mcp.Net.Core.JsonRpc;
using Mcp.Net.Core.Models.Capabilities;
using Mcp.Net.Core.Models.Content;
using Mcp.Net.Core.Models.Exceptions;
using Mcp.Net.Core.Models.Messages;
using Mcp.Net.Core.Models.Prompts;
using Mcp.Net.Core.Models.Resources;
using Mcp.Net.Core.Models.Tools;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.Client;

/// <summary>
/// An MCP client that communicates with a server using standard input/output streams.
/// </summary>
public class StdioMcpClient : McpClient
{
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _readTask;
    private readonly Stream _input;
    private readonly Stream _output;
    private Process? _serverProcess;

    /// <summary>
    /// Initializes a new instance of the StdioMcpClient class using system console streams.
    /// </summary>
    /// <param name="clientName">The name of the client.</param>
    /// <param name="clientVersion">The version of the client.</param>
    /// <param name="logger">Optional logger for client events.</param>
    public StdioMcpClient(
        string clientName = "StdioClient",
        string clientVersion = "1.0.0",
        ILogger? logger = null
    ) : base(clientName, clientVersion, logger)
    {
        _input = Console.OpenStandardInput();
        _output = Console.OpenStandardOutput();
        _readTask = ListenToInputAsync();
    }

    /// <summary>
    /// Initializes a new instance of the StdioMcpClient class using custom IO streams.
    /// </summary>
    /// <param name="inputStream">The input stream to read from.</param>
    /// <param name="outputStream">The output stream to write to.</param>
    /// <param name="clientName">The name of the client.</param>
    /// <param name="clientVersion">The version of the client.</param>
    /// <param name="logger">Optional logger for client events.</param>
    public StdioMcpClient(
        Stream inputStream,
        Stream outputStream,
        string clientName = "StdioClient",
        string clientVersion = "1.0.0",
        ILogger? logger = null
    ) : base(clientName, clientVersion, logger)
    {
        _input = inputStream ?? throw new ArgumentNullException(nameof(inputStream));
        _output = outputStream ?? throw new ArgumentNullException(nameof(outputStream));
        _readTask = ListenToInputAsync();
    }

    /// <summary>
    /// Initializes a new instance of the StdioMcpClient class using a server process.
    /// </summary>
    /// <param name="serverCommand">The command to start the server process.</param>
    /// <param name="clientName">The name of the client.</param>
    /// <param name="clientVersion">The version of the client.</param>
    /// <param name="logger">Optional logger for client events.</param>
    public StdioMcpClient(
        string serverCommand,
        string clientName = "StdioClient",
        string clientVersion = "1.0.0",
        ILogger? logger = null
    ) : base(clientName, clientVersion, logger)
    {
        if (string.IsNullOrEmpty(serverCommand))
            throw new ArgumentNullException(nameof(serverCommand));

        _serverProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/sh",
                Arguments = $"-c \"{serverCommand}\"",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            }
        };

        _serverProcess.Start();
        _input = _serverProcess.StandardOutput.BaseStream;
        _output = _serverProcess.StandardInput.BaseStream;
        _readTask = ListenToInputAsync();
    }

    /// <summary>
    /// Listens for incoming messages from the server.
    /// </summary>
    private async Task ListenToInputAsync()
    {
        var buffer = new byte[4096];
        var messageBuffer = new StringBuilder();

        try
        {
            _logger?.LogDebug("Starting input listener for StdioMcpClient");
            
            while (!_cts.Token.IsCancellationRequested)
            {
                var bytesRead = await _input.ReadAsync(buffer, 0, buffer.Length, _cts.Token);
                if (bytesRead == 0)
                {
                    _logger?.LogInformation("End of stream detected");
                    break;
                }

                var chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                messageBuffer.Append(chunk);

                ProcessBuffer(messageBuffer);
            }
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Input listener cancelled");
        }
        catch (Exception ex)
        {
            RaiseOnError(ex);
        }
        finally
        {
            RaiseOnClose();
        }
    }

    private void ProcessBuffer(StringBuilder buffer)
    {
        string content = buffer.ToString();
        int newlineIndex;

        while ((newlineIndex = content.IndexOf('\n')) >= 0)
        {
            string line = content.Substring(0, newlineIndex).Trim();
            content = content.Substring(newlineIndex + 1);

            if (!string.IsNullOrEmpty(line))
            {
                try
                {
                    // Parse the JSON document to determine the message type
                    using var jsonDoc = JsonDocument.Parse(line);
                    var root = jsonDoc.RootElement;

                    // Check if this has a result or error property (response)
                    if (
                        root.TryGetProperty("id", out _)
                        && (
                            root.TryGetProperty("result", out _)
                            || root.TryGetProperty("error", out _)
                        )
                    )
                    {
                        var response = JsonSerializer.Deserialize<JsonRpcResponseMessage>(line);
                        if (response != null)
                        {
                            ProcessResponse(response);
                        }
                    }
                    else
                    {
                        // We don't expect other message types as clients only receive responses
                        RaiseOnError(new Exception($"Unexpected message type: {line}"));
                    }
                }
                catch (JsonException ex)
                {
                    RaiseOnError(new Exception($"Invalid JSON message: {line}", ex));
                }
            }
        }

        buffer.Clear();
        buffer.Append(content);
    }

    /// <summary>
    /// Initializes the client by establishing a connection to the server.
    /// </summary>
    public override async Task Initialize()
    {
        _logger?.LogInformation("Initializing StdioMcpClient...");
        
        var initializeParams = new
        {
            protocolVersion = "2024-11-05",
            capabilities = new
            {
                tools = new { },
                resources = new { },
                prompts = new { },
            },
            clientInfo = _clientInfo,
        };

        var response = await SendRequest("initialize", initializeParams);

        try
        {
            var initializeResponse = DeserializeResponse<InitializeResponse>(response);
            if (initializeResponse != null)
            {
                _serverCapabilities = initializeResponse.Capabilities;
                _logger?.LogInformation(
                    "Connected to server: {ServerName} {ServerVersion}",
                    initializeResponse.ServerInfo?.Name,
                    initializeResponse.ServerInfo?.Version
                );

                // Send initialized notification
                await SendNotification("notifications/initialized");
            }
        }
        catch (Exception ex)
        {
            var error = new Exception($"Failed to parse initialization response: {ex.Message}", ex);
            RaiseOnError(error);
            throw error;
        }
    }

    /// <summary>
    /// Sends a request to the server and waits for a response.
    /// </summary>
    protected override async Task<object> SendRequest(string method, object? parameters = null)
    {
        var id = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<object>();
        _pendingRequests[id] = tcs;

        _logger?.LogDebug("Sending request: method={Method}, id={Id}", method, id);

        var request = new JsonRpcRequestMessage("2.0", id, method, parameters);

        var json = JsonSerializer.Serialize(request);
        var bytes = Encoding.UTF8.GetBytes(json + "\n");
        await _output.WriteAsync(bytes, 0, bytes.Length);
        await _output.FlushAsync();

        // Wait for the response with a timeout
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(60));
        var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

        if (completedTask == timeoutTask)
        {
            _pendingRequests.Remove(id);
            var timeoutError = new TimeoutException($"Request timed out: {method}");
            RaiseOnError(timeoutError);
            throw timeoutError;
        }

        return await tcs.Task;
    }

    /// <summary>
    /// Sends a notification to the server.
    /// </summary>
    protected override async Task SendNotification(string method, object? parameters = null)
    {
        _logger?.LogDebug("Sending notification: method={Method}", method);
        
        var notification = new JsonRpcNotificationMessage("2.0", method, parameters);

        var json = JsonSerializer.Serialize(notification);
        var bytes = Encoding.UTF8.GetBytes(json + "\n");
        await _output.WriteAsync(bytes, 0, bytes.Length);
        await _output.FlushAsync();
    }

    /// <summary>
    /// Disposes of resources used by the client.
    /// </summary>
    public override void Dispose()
    {
        _logger?.LogInformation("Disposing StdioMcpClient...");
        
        _cts.Cancel();
        
        try 
        {
            // Wait for read task to complete with a timeout
            if (!_readTask.IsCompleted)
            {
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
                Task.WhenAny(_readTask, timeoutTask).Wait();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error while waiting for read task to complete");
        }

        // Clean up server process if this client launched it
        if (_serverProcess != null)
        {
            try
            {
                if (!_serverProcess.HasExited)
                {
                    _serverProcess.Kill();
                }
                _serverProcess.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error while disposing server process");
            }
        }
    }
}
