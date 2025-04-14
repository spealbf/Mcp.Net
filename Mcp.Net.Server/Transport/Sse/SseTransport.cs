using Mcp.Net.Core.Interfaces;
using Mcp.Net.Core.JsonRpc;
using Mcp.Net.Core.Transport;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.Server.Transport.Sse;

/// <summary>
/// Transport implementation for Server-Sent Events (SSE)
/// </summary>
public class SseTransport : HttpTransportBase
{
    // Cache the SSE event format for better performance
    private const string SSE_DATA_FORMAT = "data: {0}\n\n";
    private const string SSE_EVENT_FORMAT = "event: {0}\ndata: {1}\n\n";

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
    ) : base(writer, parser ?? new JsonRpcMessageParser(), logger)
    {
        // Set up SSE headers
        writer.SetHeader("Content-Type", "text/event-stream");
        writer.SetHeader("Cache-Control", "no-cache");
        writer.SetHeader("Connection", "keep-alive");
    }

    /// <inheritdoc />
    public override async Task StartAsync()
    {
        if (IsStarted)
        {
            throw new InvalidOperationException("SSE transport already started");
        }

        // Send the endpoint URL as the first message
        var endpointUrl = $"/messages?sessionId={SessionId}";

        // Format as SSE event
        await SendEventAsync("endpoint", endpointUrl);

        Logger.LogDebug("SSE transport started, sent endpoint: {Endpoint}", endpointUrl);
        IsStarted = true;
        return;
    }

    /// <inheritdoc />
    public override async Task SendAsync(JsonRpcResponseMessage responseMessage)
    {
        if (IsClosed)
        {
            throw new InvalidOperationException("Transport is closed");
        }

        try
        {
            string serialized = SerializeMessage(responseMessage);
            Logger.LogDebug("Sending response over SSE: {ResponseId}", responseMessage.Id);

            // Format as SSE data
            await SendDataAsync(serialized);

            // Log response details
            bool isError = responseMessage.Error != null;
            Logger.LogInformation(
                "SSE response sent: Session={SessionId}, Id={ResponseId}, IsError={IsError}",
                SessionId,
                responseMessage.Id,
                isError
            );
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "Error sending message over SSE for session {SessionId}",
                SessionId
            );
            RaiseOnError(ex);
            throw;
        }
    }

    /// <summary>
    /// Sends data as an SSE data-only event
    /// </summary>
    /// <param name="data">The data to send</param>
    private async Task SendDataAsync(string data)
    {
        string sseData = string.Format(SSE_DATA_FORMAT, data);
        await ResponseWriter.WriteAsync(sseData, CancellationTokenSource.Token);
        await ResponseWriter.FlushAsync(CancellationTokenSource.Token);
    }

    /// <summary>
    /// Sends data as a named SSE event
    /// </summary>
    /// <param name="eventName">The event name</param>
    /// <param name="data">The event data</param>
    private async Task SendEventAsync(string eventName, string data)
    {
        string sseEvent = string.Format(SSE_EVENT_FORMAT, eventName, data);
        await ResponseWriter.WriteAsync(sseEvent, CancellationTokenSource.Token);
        await ResponseWriter.FlushAsync(CancellationTokenSource.Token);
    }
}