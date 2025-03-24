using System.Text.Json;
using Mcp.Net.Client.Interfaces;
using Mcp.Net.Core.JsonRpc;
using Mcp.Net.Core.Models.Messages;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.Client.Transport;

/// <summary>
/// Implements an MCP client that uses Server-Sent Events (SSE) for communication.
/// </summary>
public class SseMcpClient : McpClient
{
    private readonly HttpClient _httpClient;
    private readonly CancellationTokenSource _cts;
    private readonly Task _sseTask;
    private Uri? _endpoint;
    private InitializeResponse? _initializeResponse;

    public SseMcpClient(
        string baseUrl, 
        string clientName, 
        string clientVersion, 
        ILogger? logger = null) 
        : base(clientName, clientVersion, logger)
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
        _cts = new CancellationTokenSource();
        _sseTask = ListenToServerEvents();
    }

    public SseMcpClient(
        HttpClient httpClient, 
        string clientName, 
        string clientVersion, 
        ILogger? logger = null) 
        : base(clientName, clientVersion, logger)
    {
        _httpClient = httpClient;
        _cts = new CancellationTokenSource();
        _sseTask = ListenToServerEvents();
    }

    private async Task ListenToServerEvents()
    {
        try
        {
            _logger?.LogDebug("Connecting to SSE endpoint...");
            using var response = await _httpClient.GetStreamAsync("/sse", _cts.Token);
            using var reader = new StreamReader(response);

            _logger?.LogInformation("SSE connection established, waiting for events...");

            string? eventName = null;
            string data = string.Empty;

            while (!_cts.Token.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (line == null)
                    break; // End of stream

                _logger?.LogTrace("SSE Raw: {Line}", line);

                if (string.IsNullOrEmpty(line))
                {
                    // Empty line signals end of event
                    if (!string.IsNullOrEmpty(data))
                    {
                        _logger?.LogDebug("Processing event: {EventName} with data: {Data}", 
                            eventName ?? "message", data);
                        ProcessEvent(eventName, data);
                        eventName = null;
                        data = string.Empty;
                    }
                    continue;
                }

                if (line.StartsWith("event: "))
                {
                    eventName = line.Substring(7);
                }
                else if (line.StartsWith("data: "))
                {
                    data = line.Substring(6);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation, ignore
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SSE connection error: {ExceptionType}: {ExceptionMessage}", 
                ex.GetType().Name, ex.Message);
            RaiseOnError(ex);
            RaiseOnClose();
        }
    }

    private void ProcessEvent(string? eventName, string data)
    {
        _logger?.LogDebug("Processing event: {EventName}, data: {Data}", eventName, data);

        if (eventName == "endpoint")
        {
            // The endpoint should be a path like "/messages?sessionId=xxx"
            _logger?.LogDebug("Received endpoint: {Endpoint}", data);
            if (data.StartsWith("/"))
            {
                // It's a relative path, construct a full URI
                _endpoint = new Uri(_httpClient.BaseAddress!, data);
                _logger?.LogDebug("Set endpoint to: {Endpoint}", _endpoint);
            }
            else
            {
                // Try to parse as absolute URI first
                if (Uri.TryCreate(data, UriKind.Absolute, out var absoluteUri))
                {
                    _endpoint = absoluteUri;
                }
                else
                {
                    // Otherwise treat as relative
                    _endpoint = new Uri(_httpClient.BaseAddress!, data);
                }
                _logger?.LogDebug("Set endpoint to: {Endpoint}", _endpoint);
            }
            return;
        }

        try
        {
            // Parse the JSON document to determine the message type
            using var jsonDoc = JsonDocument.Parse(data);
            var root = jsonDoc.RootElement;

            // Check if this has a result or error property (response)
            if (
                root.TryGetProperty("id", out _)
                && (root.TryGetProperty("result", out _) || root.TryGetProperty("error", out _))
            )
            {
                var response = JsonSerializer.Deserialize<JsonRpcResponseMessage>(data);
                if (response == null)
                    return;

                ProcessResponse(response);
            }
            else
            {
                _logger?.LogWarning("Received unexpected message format, not a response");
            }
        }
        catch (JsonException ex)
        {
            _logger?.LogError(ex, "Error parsing message: {ErrorMessage}", ex.Message);
            RaiseOnError(ex);
        }
    }

    public override async Task Initialize()
    {
        if (_endpoint == null)
        {
            var timeout = TimeSpan.FromSeconds(10);
            _logger?.LogInformation("Waiting up to {TimeoutSeconds} seconds for endpoint...", 
                timeout.TotalSeconds);
            
            var startTime = DateTime.UtcNow;
            while (_endpoint == null)
            {
                if (DateTime.UtcNow - startTime > timeout)
                {
                    throw new TimeoutException("Timed out waiting for SSE endpoint");
                }

                await Task.Delay(100);
            }
        }

        _logger?.LogInformation("Sending initialize request...");

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
                _initializeResponse = initializeResponse;

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
        if (_endpoint == null)
        {
            throw new InvalidOperationException("SSE endpoint not received yet");
        }

        var id = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<object>();
        _pendingRequests[id] = tcs;

        object? paramsObject = parameters;

        var request = new JsonRpcRequestMessage("2.0", id, method, paramsObject);

        var requestJson = JsonSerializer.Serialize(request);
        _logger?.LogDebug("Sending request: {Method} to {Endpoint}", method, _endpoint);

        // Post the request to the message endpoint
        var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(_endpoint, content);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(
                $"HTTP error: {(int)response.StatusCode} {response.StatusCode} - {await response.Content.ReadAsStringAsync()}"
            );
        }

        // The actual response will come via SSE
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
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
        if (_endpoint == null)
        {
            throw new InvalidOperationException("SSE endpoint not received yet");
        }

        JsonElement? paramsElement = null;
        if (parameters != null)
        {
            paramsElement = JsonSerializer.SerializeToElement(parameters);
        }

        var notification = new JsonRpcNotificationMessage("2.0", method, paramsElement);

        var notificationJson = JsonSerializer.Serialize(notification);
        _logger?.LogDebug("Sending notification: {Method} to {Endpoint}", method, _endpoint);

        // Post the notification to the message endpoint
        var content = new StringContent(
            notificationJson,
            System.Text.Encoding.UTF8,
            "application/json"
        );
        var response = await _httpClient.PostAsync(_endpoint, content);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(
                $"HTTP error: {(int)response.StatusCode} {response.StatusCode} - {await response.Content.ReadAsStringAsync()}"
            );
        }
    }

    public override void Dispose()
    {
        _cts.Cancel();
        _httpClient.Dispose();
    }
}