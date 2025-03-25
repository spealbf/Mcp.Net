using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
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
    private readonly TaskCompletionSource<string> _sessionIdTcs = new();
    
    // Reuse serializer options to avoid repeated allocations
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };
    
    // SSE prefix byte arrays - create once and reuse
    private static readonly byte[] EventPrefix = Encoding.UTF8.GetBytes("event: ");
    private static readonly byte[] DataPrefix = Encoding.UTF8.GetBytes("data: ");

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

            // Wait for session ID to be received via SSE
            await WaitForSessionIdAsync(TimeSpan.FromSeconds(10));

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
    /// Waits for a session ID to be received from the server
    /// </summary>
    private async Task WaitForSessionIdAsync(TimeSpan timeout)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        try
        {
            // Wait for the session ID using TaskCompletionSource
            await _sessionIdTcs.Task.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"Timed out waiting for session ID from server");
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

                _isConnected = true;
            }
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    /// <summary>
    /// Listens for Server-Sent Events from the server using System.IO.Pipelines.
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
            var pipeReader = PipeReader.Create(stream);

            try
            {
                // Keep track of the current event and data
                string? currentEvent = null;
                StringBuilder dataBuilder = new();

                while (!_cts.Token.IsCancellationRequested)
                {
                    ReadResult result = await pipeReader.ReadAsync(_cts.Token);
                    ReadOnlySequence<byte> buffer = result.Buffer;
                    
                    ProcessSseBuffer(buffer, ref currentEvent, dataBuilder);

                    // Tell the pipe reader how much of the buffer we consumed
                    pipeReader.AdvanceTo(buffer.Start, buffer.End);

                    if (result.IsCompleted)
                    {
                        break;
                    }
                }
            }
            finally
            {
                await pipeReader.CompleteAsync();
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
    /// Processes an SSE buffer to extract events and data
    /// </summary>
    private void ProcessSseBuffer(
        ReadOnlySequence<byte> buffer, 
        ref string? currentEvent, 
        StringBuilder dataBuilder)
    {
        // Process the buffer line by line
        SequenceReader<byte> reader = new(buffer);
        
        while (reader.TryReadTo(out ReadOnlySequence<byte> line, (byte)'\n'))
        {
            // Skip empty lines (end of event)
            if (line.Length == 0)
            {
                if (dataBuilder.Length > 0)
                {
                    string data = dataBuilder.ToString();
                    dataBuilder.Clear();
                    
                    // Handle the complete event
                    HandleSseEvent(currentEvent, data);
                    currentEvent = null;
                }
                continue;
            }
            
            // Get the line as span
            ReadOnlySpan<byte> lineSpan;
            if (line.IsSingleSegment)
            {
                lineSpan = line.First.Span;
            }
            else
            {
                lineSpan = line.ToArray();
            }
            
            // Check for event: prefix
            if (StartsWith(lineSpan, EventPrefix))
            {
                var eventValueBytes = new byte[lineSpan.Length - EventPrefix.Length];
                lineSpan.Slice(EventPrefix.Length).CopyTo(eventValueBytes);
                currentEvent = Encoding.UTF8.GetString(eventValueBytes);
                continue;
            }
            
            // Check for data: prefix
            if (StartsWith(lineSpan, DataPrefix))
            {
                var dataValueBytes = new byte[lineSpan.Length - DataPrefix.Length];
                lineSpan.Slice(DataPrefix.Length).CopyTo(dataValueBytes);
                string data = Encoding.UTF8.GetString(dataValueBytes);
                
                if (dataBuilder.Length > 0)
                {
                    dataBuilder.AppendLine();
                }
                dataBuilder.Append(data);
            }
        }
    }
    
    /// <summary>
    /// Helper method to check if a span starts with a specific sequence
    /// </summary>
    private static bool StartsWith(ReadOnlySpan<byte> span, byte[] value)
    {
        if (span.Length < value.Length)
            return false;
            
        for (int i = 0; i < value.Length; i++)
        {
            if (span[i] != value[i])
                return false;
        }
        
        return true;
    }

    /// <summary>
    /// Handles a complete SSE event with its data
    /// </summary>
    private void HandleSseEvent(string? eventType, string data)
    {
        try
        {
            _logger?.LogDebug("Processing SSE event: {EventType}, data: {Data}", 
                eventType ?? "message", data);
            
            // Handle endpoint events
            if (eventType == "endpoint")
            {
                HandleEndpointEvent(data);
                return;
            }
            
            // For data events without an event type, try to parse as JSON-RPC
            if (string.IsNullOrEmpty(eventType) && !string.IsNullOrWhiteSpace(data))
            {
                HandleJsonRpcEvent(data);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing SSE event: {EventType}", eventType);
            RaiseOnError(new Exception($"Error processing SSE event: {ex.Message}", ex));
        }
    }

    /// <summary>
    /// Handles endpoint events from the server
    /// </summary>
    private void HandleEndpointEvent(string data)
    {
        if (data.StartsWith("/messages?sessionId="))
        {
            _logger?.LogDebug("Received endpoint URL: {Endpoint}", data);
            _messagesEndpoint = data;
            _sessionId = data.Substring("/messages?sessionId=".Length);
            _logger?.LogDebug("Extracted session ID: {SessionId}", _sessionId);
            
            // Complete the task to signal that session ID is available
            _sessionIdTcs.TrySetResult(_sessionId);
        }
        else if (Uri.TryCreate(data, UriKind.Absolute, out var uri))
        {
            _logger?.LogDebug("Received absolute endpoint URL: {Endpoint}", data);
            _messagesEndpoint = data;
            
            // Try to extract session ID from query string
            var queryParams = uri.Query.TrimStart('?').Split('&');
            foreach (var param in queryParams)
            {
                if (param.StartsWith("sessionId="))
                {
                    _sessionId = param.Substring("sessionId=".Length);
                    _logger?.LogDebug("Extracted session ID from URL: {SessionId}", _sessionId);
                    
                    // Complete the task to signal that session ID is available
                    _sessionIdTcs.TrySetResult(_sessionId);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Handles JSON-RPC messages received via SSE
    /// </summary>
    private void HandleJsonRpcEvent(string data)
    {
        try
        {
            // Quick check if this looks like a JSON-RPC response
            if (!data.Contains("\"jsonrpc\"") || !data.Contains("\"id\""))
            {
                return;
            }
            
            // Try to parse as JSON-RPC response
            var response = JsonSerializer.Deserialize<JsonRpcResponseMessage>(
                data, _serializerOptions);
                
            if (response != null)
            {
                _logger?.LogDebug("Successfully parsed JSON-RPC response: {Id}", response.Id);
                ProcessResponse(response);
            }
        }
        catch (JsonException ex)
        {
            _logger?.LogWarning(ex, "Failed to parse as JSON-RPC response: {Data}", 
                data.Length > 100 ? data.Substring(0, 97) + "..." : data);
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

        // Ensure we have a session ID
        if (string.IsNullOrEmpty(_sessionId))
        {
            await WaitForSessionIdAsync(TimeSpan.FromSeconds(10));
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
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        
        try
        {
            // Wait for the response using TaskCompletionSource
            return await tcs.Task.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            _pendingRequests.Remove(id);
            var timeoutError = new TimeoutException($"Request timed out: {method}");
            RaiseOnError(timeoutError);
            throw timeoutError;
        }
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

        // Ensure we have a session ID
        if (string.IsNullOrEmpty(_sessionId))
        {
            await WaitForSessionIdAsync(TimeSpan.FromSeconds(10));
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
                Task.WaitAny(new[] { _sseListenerTask }, TimeSpan.FromSeconds(5));
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