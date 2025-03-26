namespace Mcp.Net.Server.Transport.Sse;

/// <summary>
/// Middleware that handles SSE messages
/// </summary>
public class SseMessageMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SseConnectionManager _connectionManager;
    private readonly ILogger<SseMessageMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SseMessageMiddleware"/> class
    /// </summary>
    /// <param name="next">The next middleware in the pipeline</param>
    /// <param name="connectionManager">The SSE connection manager</param>
    /// <param name="logger">Logger for SSE message events</param>
    public SseMessageMiddleware(
        RequestDelegate next,
        SseConnectionManager connectionManager,
        ILogger<SseMessageMiddleware> logger
    )
    {
        _next = next;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    /// <summary>
    /// Processes an HTTP request for SSE message
    /// </summary>
    /// <param name="context">The HTTP context for the request</param>
    public async Task InvokeAsync(HttpContext context)
    {
        _logger.LogDebug(
            "Handling SSE message from {ClientIp}",
            context.Connection.RemoteIpAddress
        );

        // Authentication is already performed by McpAuthenticationMiddleware
        await _connectionManager.HandleMessageAsync(context);
    }
}
