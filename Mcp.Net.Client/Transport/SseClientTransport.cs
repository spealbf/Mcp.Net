using System.Text;
using System.Text.Json;
using Mcp.Net.Core.JsonRpc;
using Mcp.Net.Core.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mcp.Net.Client.Transport;

/// <summary>
/// Client transport implementation that uses Server-Sent Events (SSE) for receiving data
/// and HTTP POST for sending data.
/// </summary>
public class SseClientTransport : ClientTransportBase
{
    private readonly HttpClient _httpClient;
    private Uri? _messageEndpoint;
    private Task? _sseListenTask;
    private readonly TimeSpan _endpointWaitTimeout = TimeSpan.FromSeconds(10);
    private readonly ManualResetEventSlim _endpointReceived = new ManualResetEventSlim(false);
    private readonly string? _apiKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="SseClientTransport"/> class.
    /// </summary>
    /// <param name="baseUrl">The base URL of the SSE server.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="apiKey">Optional API key for authentication.</param>
    public SseClientTransport(string baseUrl, ILogger? logger = null, string? apiKey = null)
        : this(new HttpClient { BaseAddress = new Uri(baseUrl) }, logger, apiKey) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SseClientTransport"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for communication.</param>
    /// <param name="logger">Optional logger.</param>
    /// <param name="apiKey">Optional API key for authentication.</param>
    public SseClientTransport(HttpClient httpClient, ILogger? logger = null, string? apiKey = null)
        : base(new JsonRpcMessageParser(), logger ?? NullLogger.Instance)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKey = apiKey;

        // Add API key to default headers if provided
        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
        }
    }

    /// <inheritdoc />
    public override async Task StartAsync()
    {
        if (IsStarted)
        {
            throw new InvalidOperationException("Transport already started");
        }

        IsStarted = true;
        _sseListenTask = ListenToServerEvents();
        Logger.LogDebug("SseClientTransport started");

        // Wait for the endpoint to be received
        await Task.Run(() =>
        {
            if (!_endpointReceived.Wait(_endpointWaitTimeout))
            {
                Logger.LogError("Timed out waiting for SSE endpoint");
                throw new TimeoutException("Timed out waiting for SSE endpoint");
            }
        });
    }

    /// <inheritdoc />
    public override async Task<object> SendRequestAsync(string method, object? parameters = null)
    {
        if (IsClosed)
        {
            throw new InvalidOperationException("Transport is closed");
        }

        if (_messageEndpoint == null)
        {
            throw new InvalidOperationException("SSE endpoint not received yet");
        }

        // Create a unique ID for this request
        var id = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<object>();
        PendingRequests[id] = tcs;

        // Create the request message
        var request = new JsonRpcRequestMessage("2.0", id, method, parameters);

        try
        {
            // Send the request via HTTP POST
            var requestJson = SerializeMessage(request);
            Logger.LogDebug("Sending request: {Method} to {Endpoint}", method, _messageEndpoint);

            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(
                _messageEndpoint,
                content,
                CancellationTokenSource.Token
            );

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(
                    $"HTTP error: {(int)response.StatusCode} {response.StatusCode} - {await response.Content.ReadAsStringAsync(CancellationTokenSource.Token)}"
                );
            }

            // The actual response will come via SSE
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30), CancellationTokenSource.Token);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                PendingRequests.TryRemove(id, out _);
                throw new TimeoutException($"Request timed out: {method}");
            }

            return await tcs.Task;
        }
        catch (OperationCanceledException)
        {
            PendingRequests.TryRemove(id, out _);
            throw;
        }
        catch (Exception ex)
        {
            PendingRequests.TryRemove(id, out _);
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

        if (_messageEndpoint == null)
        {
            throw new InvalidOperationException("SSE endpoint not received yet");
        }

        // Create the notification message
        var notification = new JsonRpcNotificationMessage(
            "2.0",
            method,
            parameters != null ? JsonSerializer.SerializeToElement(parameters) : null
        );

        try
        {
            // Send the notification via HTTP POST
            var notificationJson = SerializeMessage(notification);
            Logger.LogDebug(
                "Sending notification: {Method} to {Endpoint}",
                method,
                _messageEndpoint
            );

            var content = new StringContent(notificationJson, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(
                _messageEndpoint,
                content,
                CancellationTokenSource.Token
            );

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(
                    $"HTTP error: {(int)response.StatusCode} {response.StatusCode} - {await response.Content.ReadAsStringAsync(CancellationTokenSource.Token)}"
                );
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error sending notification: {Method}", method);
            throw;
        }
    }

    private async Task ListenToServerEvents()
    {
        try
        {
            Logger.LogDebug("Connecting to SSE endpoint...");
            using var response = await _httpClient.GetStreamAsync(
                "/sse",
                CancellationTokenSource.Token
            );
            using var reader = new StreamReader(response);

            Logger.LogInformation("SSE connection established, waiting for events...");

            string? eventName = null;
            string data = string.Empty;

            while (!CancellationTokenSource.Token.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (line == null)
                    break; // End of stream

                Logger.LogTrace("SSE Raw: {Line}", line);

                if (string.IsNullOrEmpty(line))
                {
                    // Empty line signals end of event
                    if (!string.IsNullOrEmpty(data))
                    {
                        Logger.LogDebug(
                            "Processing event: {EventName} with data: {Data}",
                            eventName ?? "message",
                            data
                        );
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
            // Normal cancellation
            Logger.LogDebug("SSE connection cancelled");
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "SSE connection error: {ExceptionType}: {ExceptionMessage}",
                ex.GetType().Name,
                ex.Message
            );
            RaiseOnError(ex);
        }
        finally
        {
            Logger.LogInformation("SSE listen task terminated");
        }
    }

    private void ProcessEvent(string? eventName, string data)
    {
        Logger.LogDebug("Processing event: {EventName}, data: {Data}", eventName, data);

        // Handle the special "endpoint" event
        if (eventName == "endpoint")
        {
            // The endpoint should be a path like "/messages?sessionId=xxx"
            Logger.LogDebug("Received endpoint: {Endpoint}", data);

            Uri? endpoint = null;
            if (data.StartsWith("/"))
            {
                // It's a relative path, construct a full URI
                endpoint = new Uri(_httpClient.BaseAddress!, data);
            }
            else
            {
                // Try to parse as absolute URI first
                if (Uri.TryCreate(data, UriKind.Absolute, out var absoluteUri))
                {
                    endpoint = absoluteUri;
                }
                else
                {
                    // Otherwise treat as relative
                    endpoint = new Uri(_httpClient.BaseAddress!, data);
                }
            }

            _messageEndpoint = endpoint;
            Logger.LogDebug("Set endpoint to: {Endpoint}", _messageEndpoint);
            _endpointReceived.Set();
            return;
        }

        // For other events, process as JSON-RPC message
        ProcessJsonRpcMessage(data);
    }

    /// <inheritdoc />
    protected override async Task OnClosingAsync()
    {
        await base.OnClosingAsync();
        _endpointReceived.Dispose();
    }
}
