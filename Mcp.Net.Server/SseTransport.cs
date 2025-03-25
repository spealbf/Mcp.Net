using System.Text.Json;
using Mcp.Net.Core.Interfaces;
using Mcp.Net.Core.JsonRpc;

namespace Mcp.Net.Server;

/// <summary>
/// Transport implementation for Server-Sent Events (SSE)
/// </summary>
public class SseTransport : ITransport
{
    private readonly IResponseWriter _writer;
    private readonly IMessageParser _parser;
    private readonly ILogger<SseTransport> _logger;
    private readonly CancellationTokenSource _cts = new();
    private bool _closed = false;

    // Reuse serializer options to avoid repeated allocations
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <inheritdoc />
    public event Action<JsonRpcRequestMessage>? OnRequest;

    /// <inheritdoc />
    public event Action<JsonRpcNotificationMessage>? OnNotification;

    /// <inheritdoc />
    public event Action<Exception>? OnError;

    /// <inheritdoc />
    public event Action? OnClose;

    /// <summary>
    /// Gets the session ID for this transport
    /// </summary>
    public string SessionId => _writer.Id;

    /// <summary>
    /// Initializes a new instance of the <see cref="SseTransport"/> class
    /// </summary>
    /// <param name="writer">Response writer for SSE</param>
    /// <param name="logger">Logger</param>
    /// <param name="parser">Optional message parser</param>
    public SseTransport(
        IResponseWriter writer,
        ILogger<SseTransport> logger,
        IMessageParser? parser = null
    )
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _parser = parser ?? new JsonRpcMessageParser();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Set up SSE headers
        _writer.SetHeader("Content-Type", "text/event-stream");
        _writer.SetHeader("Cache-Control", "no-cache");
        _writer.SetHeader("Connection", "keep-alive");
    }

    /// <inheritdoc />
    public async Task StartAsync()
    {
        // Send the endpoint URL as the first message
        var endpointUrl = $"/messages?sessionId={_writer.Id}";

        // Format as SSE event
        var sseEvent = $"event: endpoint\ndata: {endpointUrl}\n\n";
        await _writer.WriteAsync(sseEvent, _cts.Token);
        await _writer.FlushAsync(_cts.Token);

        _logger.LogDebug("SSE transport started, sent endpoint: {Endpoint}", endpointUrl);
        return;
    }

    /// <inheritdoc />
    public async Task SendAsync(JsonRpcResponseMessage responseMessage)
    {
        if (_closed)
        {
            throw new InvalidOperationException("Transport is closed");
        }

        try
        {
            string serialized = JsonSerializer.Serialize(responseMessage, _serializerOptions);
            _logger.LogDebug("Sending response over SSE: {ResponseId}", responseMessage.Id);

            await _writer.WriteAsync($"data: {serialized}\n\n", _cts.Token);
            await _writer.FlushAsync(_cts.Token);

            // Log response details
            bool isError = responseMessage.Error != null;
            _logger.LogInformation(
                "SSE response sent: Session={SessionId}, Id={ResponseId}, IsError={IsError}",
                _writer.Id,
                responseMessage.Id,
                isError
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error sending message over SSE for session {SessionId}",
                _writer.Id
            );
            OnError?.Invoke(ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task CloseAsync()
    {
        if (!_closed)
        {
            _closed = true;
            _logger.LogInformation("Closing SSE transport for session {SessionId}", _writer.Id);

            // Cancel all operations
            _cts.Cancel();

            // Complete the response writer
            await _writer.CompleteAsync();

            OnClose?.Invoke();
        }
    }

    /// <summary>
    /// Handles a JSON-RPC request from an HTTP POST message
    /// </summary>
    /// <param name="requestMessage">The JSON-RPC request message</param>
    internal void HandlePostMessage(JsonRpcRequestMessage requestMessage)
    {
        if (requestMessage != null && !string.IsNullOrEmpty(requestMessage.Method))
        {
            _logger.LogDebug(
                "Handling POST message: Method={Method}, Id={RequestId}",
                requestMessage.Method,
                requestMessage.Id
            );

            OnRequest?.Invoke(requestMessage);
        }
        else
        {
            _logger.LogWarning("Received invalid request with missing method or null request");
        }
    }

    /// <summary>
    /// Handles a JSON-RPC notification from an HTTP POST message
    /// </summary>
    /// <param name="notificationMessage">The JSON-RPC notification message</param>
    internal void HandlePostNotification(JsonRpcNotificationMessage notificationMessage)
    {
        if (notificationMessage != null && !string.IsNullOrEmpty(notificationMessage.Method))
        {
            _logger.LogDebug(
                "Handling POST notification: Method={Method}",
                notificationMessage.Method
            );

            OnNotification?.Invoke(notificationMessage);
        }
        else
        {
            _logger.LogWarning(
                "Received invalid notification with missing method or null notification"
            );
        }
    }
}
