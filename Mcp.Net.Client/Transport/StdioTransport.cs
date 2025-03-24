using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Mcp.Net.Client.Interfaces;
using Mcp.Net.Core.JsonRpc;
using Mcp.Net.Core.Models.Messages;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.Client.Transport;

/// <summary>
/// Implements an MCP client that uses Standard Input/Output for communication.
/// </summary>
public class StdioMcpClient : McpClient
{
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _readTask;
    private readonly Stream _input;
    private readonly Stream _output;
    private readonly Process? _serverProcess;

    public StdioMcpClient(
        string clientName = "StdioClient",
        string clientVersion = "1.0.0",
        ILogger? logger = null)
        : base(clientName, clientVersion, logger)
    {
        _input = Console.OpenStandardInput();
        _output = Console.OpenStandardOutput();
        _readTask = ListenToInputAsync();
    }

    public StdioMcpClient(
        Stream input,
        Stream output,
        string clientName = "StdioClient",
        string clientVersion = "1.0.0",
        ILogger? logger = null)
        : base(clientName, clientVersion, logger)
    {
        _input = input;
        _output = output;
        _readTask = ListenToInputAsync();
    }

    public StdioMcpClient(
        string serverCommand,
        string clientName = "StdioClient",
        string clientVersion = "1.0.0",
        ILogger? logger = null)
        : base(clientName, clientVersion, logger)
    {
        _logger?.LogInformation("Launching server: {ServerCommand}", serverCommand);

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
                _logger?.LogInformation("[SERVER] {ErrorData}", e.Data);
            }
        };

        _serverProcess.Start();
        _serverProcess.BeginErrorReadLine();

        _input = _serverProcess.StandardOutput.BaseStream;
        _output = _serverProcess.StandardInput.BaseStream;
        _readTask = ListenToInputAsync();
    }

    private async Task ListenToInputAsync()
    {
        var buffer = new byte[4096];
        var messageBuffer = new StringBuilder();

        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                var bytesRead = await _input.ReadAsync(buffer, 0, buffer.Length, _cts.Token);
                if (bytesRead == 0)
                {
                    // End of stream
                    break;
                }

                var chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                messageBuffer.Append(chunk);

                ProcessBuffer(messageBuffer);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error reading from input stream: {ErrorMessage}", ex.Message);
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
                        _logger?.LogWarning("Unexpected message type: {Line}", line);
                        RaiseOnError(new Exception($"Unexpected message type: {line}"));
                    }
                }
                catch (JsonException ex)
                {
                    _logger?.LogError(ex, "Invalid JSON message: {Line}", line);
                    RaiseOnError(new Exception($"Invalid JSON message: {line}", ex));
                }
            }
        }

        buffer.Clear();
        buffer.Append(content);
    }

    public override async Task Initialize()
    {
        _logger?.LogInformation("Initializing stdio connection...");
        
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
            throw new Exception($"Failed to parse initialization response: {ex.Message}", ex);
        }
    }

    protected override async Task<object> SendRequest(string method, object? parameters = null)
    {
        var id = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<object>();
        _pendingRequests[id] = tcs;

        object? paramsObject = parameters;

        var request = new JsonRpcRequestMessage("2.0", id, method, paramsObject);

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
            throw new TimeoutException($"Request timed out: {method}");
        }

        return await tcs.Task;
    }

    protected override async Task SendNotification(string method, object? parameters = null)
    {
        object? paramsObject = parameters;

        var notification = new JsonRpcNotificationMessage("2.0", method, paramsObject);

        var json = JsonSerializer.Serialize(notification);
        var bytes = Encoding.UTF8.GetBytes(json + "\n");
        await _output.WriteAsync(bytes, 0, bytes.Length);
        await _output.FlushAsync();
    }

    public override void Dispose()
    {
        _cts.Cancel();
        
        if (_serverProcess != null && !_serverProcess.HasExited)
        {
            _logger?.LogInformation("Terminating server process...");
            try
            {
                _serverProcess.Kill(true);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error terminating server process: {ErrorMessage}", ex.Message);
            }
        }
    }
}