using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Mcp.Net.Client.Interfaces;
using Mcp.Net.Core.JsonRpc;
using Mcp.Net.Core.Models.Capabilities;
using Mcp.Net.Core.Models.Messages;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.Client;

/// <summary>
/// An MCP client that communicates with a server using Server-Sent Events (SSE).
/// </summary>
public class SseMcpClient : McpClient
{
    private readonly HttpClient _httpClient;
    private readonly string? _serverUrl;
    private readonly CancellationTokenSource _cts = new();
    private Task? _sseListenerTask;
    private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);
    private bool _isConnected;
    private string? _sessionId;
    private string? _messagesEndpoint;

    /// <summary>
    /// Initializes a new instance of the SseMcpClient class with a specified URL.
    /// </summary>
    /// <param name="serverUrl">The URL of the server.</param>
    /// <param name="clientName">The name of the client.</param>
    /// <param name="clientVersion">The version of the client.</param>
    /// <param name="logger">Optional logger for client events.</param>
    public SseMcpClient(
        string serverUrl,
        string clientName = "SseClient",
        string clientVersion = "1.0.0",
        ILogger? logger = null
    )
        : base(clientName, clientVersion, logger)
    {
        _serverUrl = serverUrl;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
    }

    /// <summary>
    /// Initializes a new instance of the SseMcpClient class with a specified HttpClient.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use.</param>
    /// <param name="clientName">The name of the client.</param>
    /// <param name="clientVersion">The version of the client.</param>
    /// <param name="logger">Optional logger for client events.</param>
    public SseMcpClient(
        HttpClient httpClient,
        string clientName = "SseClient",
        string clientVersion = "1.0.0",
        ILogger? logger = null
    )
        : base(clientName, clientVersion, logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Initializes the client by establishing a connection to the server.
    /// </summary>
    public override async Task Initialize()
    {
        if (_serverUrl == null && _httpClient.BaseAddress == null)
        {
            throw new InvalidOperationException(
                "Server URL not set. Please provide a server URL or set the HttpClient.BaseAddress."
            );
        }

        _logger?.LogInformation("Initializing SseMcpClient...");

        try
        {
            // Start SSE listener if not already started
            await StartSseListenerAsync();

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
            var error = new Exception($"Failed to initialize: {ex.Message}", ex);
            RaiseOnError(error);
            throw error;
        }
    }

    /// <summary>
    /// Starts the SSE listener if it's not already running.
    /// </summary>
    private async Task StartSseListenerAsync()
    {
        await _connectionSemaphore.WaitAsync();
        try
        {
            if (!_isConnected)
            {
                _logger?.LogDebug("Starting SSE listener...");

                var baseUrl =
                    _serverUrl
                    ?? _httpClient.BaseAddress?.ToString()
                    ?? throw new InvalidOperationException("Server URL not set");
                var sseUrl = baseUrl.TrimEnd('/') + "/sse";

                _sseListenerTask = Task.Run(() => ListenForSseEventsAsync(sseUrl));

                // Wait a bit to ensure the connection is established
                await Task.Delay(500);
                _isConnected = true;
            }
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    /// <summary>
    /// Listens for Server-Sent Events from the server.
    /// </summary>
    private async Task ListenForSseEventsAsync(string sseUrl)
    {
        try
        {
            _logger?.LogDebug("Connecting to SSE endpoint: {SseUrl}", sseUrl);

            using var request = new HttpRequestMessage(HttpMethod.Get, sseUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                _cts.Token
            );

            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            var messageBuilder = new StringBuilder();
            string? line;

            while (
                !_cts.Token.IsCancellationRequested && (line = await reader.ReadLineAsync()) != null
            )
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    // Empty line means end of message
                    var message = messageBuilder.ToString();
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        ProcessSseMessage(message);
                    }
                    messageBuilder.Clear();
                }
                else if (line.StartsWith("data:"))
                {
                    // Data line
                    var data = line.Substring(5).Trim();
                    
                    // Check if this is the endpoint data following an endpoint event
                    if (data.StartsWith("/messages?sessionId="))
                    {
                        _logger?.LogDebug("Received endpoint URL: {Endpoint}", data);
                        _messagesEndpoint = data;
                        _sessionId = data.Substring("/messages?sessionId=".Length);
                        _logger?.LogDebug("Extracted session ID: {SessionId}", _sessionId);
                        // Don't add this to the message builder, it's not a JSON-RPC message
                    }
                    else
                    {
                        // Regular data, add to message builder
                        messageBuilder.AppendLine(data);
                    }
                }
                else if (line.StartsWith("event:"))
                {
                    // Handle event lines (especially for endpoint info)
                    var eventType = line.Substring(6).Trim();
                    _logger?.LogDebug("Received SSE event: {EventType}", eventType);
                    
                    // If this is the endpoint event, prepare to get the data on the next line
                    if (eventType == "endpoint")
                    {
                        // We'll receive a data: line next with the endpoint URL
                        _logger?.LogDebug("Expecting endpoint URL in next data: line");
                    }
                }
                // Ignore other lines like comments
            }
        }
        catch (TaskCanceledException)
        {
            // Normal cancellation
            _logger?.LogInformation("SSE listener cancelled");
        }
        catch (Exception ex)
        {
            if (!_cts.Token.IsCancellationRequested)
            {
                RaiseOnError(ex);
            }
        }
        finally
        {
            _isConnected = false;
            RaiseOnClose();
        }
    }

    /// <summary>
    /// Processes an SSE message.
    /// </summary>
    private void ProcessSseMessage(string message)
    {
        try
        {
            _logger?.LogDebug("Processing SSE message: {Message}", message);
            
            // If the message is empty, just return
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }
            
            // If the message is a session ID endpoint, store it for later use
            if (message.StartsWith("/messages?sessionId="))
            {
                _logger?.LogDebug("Received endpoint information: {Endpoint}", message);
                _messagesEndpoint = message;
                _sessionId = message.Substring("/messages?sessionId=".Length);
                _logger?.LogDebug("Extracted session ID from message: {SessionId}", _sessionId);
                return; // This is not a JSON-RPC message, so just return
            }
            
            // Try to parse as JSON-RPC message
            _logger?.LogDebug("Attempting to parse as JSON-RPC message: {Message}", message);
            try
            {
                var responseMessage = JsonSerializer.Deserialize<JsonRpcResponseMessage>(message);
                if (responseMessage != null)
                {
                    _logger?.LogDebug("Successfully parsed JSON-RPC response with ID: {Id}", responseMessage.Id);
                    ProcessResponse(responseMessage);
                }
            }
            catch (JsonException ex)
            {
                _logger?.LogWarning(ex, "Failed to parse as JSON-RPC message, might be a different format: {Message}", message);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing SSE message: {Message}", message);
            RaiseOnError(new Exception($"Error processing SSE message: {message}", ex));
        }
    }

    /// <summary>
    /// Sends a request to the server and waits for a response.
    /// </summary>
    protected override async Task<object> SendRequest(string method, object? parameters = null)
    {
        if (_serverUrl == null && _httpClient.BaseAddress == null)
        {
            throw new InvalidOperationException("Server URL not set");
        }

        var id = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<object>();
        _pendingRequests[id] = tcs;

        _logger?.LogDebug("Sending request: method={Method}, id={Id}", method, id);

        var request = new JsonRpcRequestMessage("2.0", id, method, parameters);

        // Wait until we have received a session ID from the server
        if (string.IsNullOrEmpty(_sessionId))
        {
            _logger?.LogDebug("Waiting for session ID before sending request...");
            int attempts = 0;
            while (string.IsNullOrEmpty(_sessionId) && attempts < 10)
            {
                await Task.Delay(200);
                attempts++;
            }
            
            if (string.IsNullOrEmpty(_sessionId))
            {
                throw new InvalidOperationException("Failed to receive session ID from server");
            }
            
            _logger?.LogDebug("Received session ID after waiting: {SessionId}", _sessionId);
        }
        
        var baseUrl =
            _serverUrl
            ?? _httpClient.BaseAddress?.ToString()
            ?? throw new InvalidOperationException("Server URL not set");
        var requestUrl = baseUrl.TrimEnd('/') + "/messages?sessionId=" + _sessionId;

        try
        {
            var response = await _httpClient.PostAsJsonAsync(requestUrl, request, _cts.Token);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _pendingRequests.Remove(id);
            var error = new Exception($"Failed to send request: {ex.Message}", ex);
            RaiseOnError(error);
            throw error;
        }

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
        if (_serverUrl == null && _httpClient.BaseAddress == null)
        {
            throw new InvalidOperationException("Server URL not set");
        }

        _logger?.LogDebug("Sending notification: method={Method}", method);

        var notification = new JsonRpcNotificationMessage("2.0", method, parameters);

        // Wait until we have received a session ID from the server
        if (string.IsNullOrEmpty(_sessionId))
        {
            _logger?.LogDebug("Waiting for session ID before sending notification...");
            int attempts = 0;
            while (string.IsNullOrEmpty(_sessionId) && attempts < 10)
            {
                await Task.Delay(200);
                attempts++;
            }
            
            if (string.IsNullOrEmpty(_sessionId))
            {
                throw new InvalidOperationException("Failed to receive session ID from server");
            }
            
            _logger?.LogDebug("Received session ID after waiting: {SessionId}", _sessionId);
        }

        var baseUrl =
            _serverUrl
            ?? _httpClient.BaseAddress?.ToString()
            ?? throw new InvalidOperationException("Server URL not set");
        var requestUrl = baseUrl.TrimEnd('/') + "/messages?sessionId=" + _sessionId;

        try
        {
            var response = await _httpClient.PostAsJsonAsync(requestUrl, notification, _cts.Token);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            var error = new Exception($"Failed to send notification: {ex.Message}", ex);
            RaiseOnError(error);
            throw error;
        }
    }

    /// <summary>
    /// Disposes of resources used by the client.
    /// </summary>
    public override void Dispose()
    {
        _logger?.LogInformation("Disposing SseMcpClient...");

        _cts.Cancel();

        try
        {
            // Wait for the SSE listener task to complete with a timeout
            if (_sseListenerTask != null && !_sseListenerTask.IsCompleted)
            {
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
                Task.WhenAny(_sseListenerTask, timeoutTask).Wait();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error while waiting for SSE listener task to complete");
        }

        _connectionSemaphore.Dispose();

        // Don't dispose the HttpClient if it was provided externally
        if (_serverUrl != null)
        {
            _httpClient.Dispose();
        }
    }
}
