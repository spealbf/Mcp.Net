using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.Server.Transport.Sse;

/// <summary>
/// Middleware that handles SSE connections
/// </summary>
public class SseConnectionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SseConnectionManager _connectionManager;
    private readonly ILogger<SseConnectionMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SseConnectionMiddleware"/> class
    /// </summary>
    /// <param name="next">The next middleware in the pipeline</param>
    /// <param name="connectionManager">The SSE connection manager</param>
    /// <param name="logger">Logger for SSE connection events</param>
    public SseConnectionMiddleware(
        RequestDelegate next,
        SseConnectionManager connectionManager,
        ILogger<SseConnectionMiddleware> logger)
    {
        _next = next;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    /// <summary>
    /// Processes an HTTP request for SSE connection
    /// </summary>
    /// <param name="context">The HTTP context for the request</param>
    public async Task InvokeAsync(HttpContext context)
    {
        _logger.LogDebug("Handling SSE connection from {ClientIp}", context.Connection.RemoteIpAddress);

        // Authentication is already performed by McpAuthenticationMiddleware
        await _connectionManager.HandleSseConnectionAsync(context);
    }
}