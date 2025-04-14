using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Mcp.Net.Server.Logging;
using System.Diagnostics;

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
        var connectionId = context.Connection.Id;
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = context.Request.Headers.UserAgent.ToString();
        
        // Create a logging scope for the entire connection lifecycle
        using (_logger.BeginConnectionScope(connectionId, clientIp))
        {
            _logger.LogInformation(
                "New SSE connection from {ClientIp} with User-Agent: {UserAgent}", 
                clientIp,
                string.IsNullOrEmpty(userAgent) ? "not provided" : userAgent);
            
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Authentication is already performed by McpAuthenticationMiddleware
                await _connectionManager.HandleSseConnectionAsync(context);
                
                stopwatch.Stop();
                _logger.LogInformation(
                    "SSE connection {ConnectionId} from {ClientIp} completed after {ConnectionDurationMs}ms", 
                    connectionId, 
                    clientIp,
                    stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogConnectionException(ex, connectionId, "SSE connection handling");
                throw; // Rethrow so the ASP.NET Core pipeline can handle it
            }
        }
    }
}