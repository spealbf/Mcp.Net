namespace Mcp.Net.Server.Authentication;

/// <summary>
/// Middleware that handles authentication for MCP endpoints
/// </summary>
public class McpAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAuthentication? _authentication;
    private readonly ILogger<McpAuthenticationMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpAuthenticationMiddleware"/> class
    /// </summary>
    /// <param name="next">The next middleware in the pipeline</param>
    /// <param name="logger">Logger for authentication events</param>
    /// <param name="authentication">Optional authentication service</param>
    public McpAuthenticationMiddleware(
        RequestDelegate next,
        ILogger<McpAuthenticationMiddleware> logger,
        IAuthentication? authentication = null
    )
    {
        _next = next;
        _authentication = authentication;
        _logger = logger;
    }

    /// <summary>
    /// Processes an HTTP request
    /// </summary>
    /// <param name="context">The HTTP context for the request</param>
    public async Task InvokeAsync(HttpContext context)
    {
        // Skip auth for non-secured endpoints
        if (!IsSecuredEndpoint(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // If authentication is disabled, continue
        if (_authentication == null)
        {
            _logger.LogWarning(
                "Authentication not configured for secured endpoint {Path}. "
                    + "This is potentially insecure. Configure authentication with UseApiKeyAuthentication().",
                context.Request.Path
            );
            await _next(context);
            return;
        }

        // Authenticate the request
        var authResult = await _authentication.AuthenticateAsync(context);
        if (!authResult.Succeeded)
        {
            _logger.LogWarning(
                "Authentication failed for {Path}: {Reason}",
                context.Request.Path,
                authResult.FailureReason
            );

            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(
                new { error = "Unauthorized", message = authResult.FailureReason }
            );
            return;
        }

        // Store auth result in context for later use
        context.Items["AuthResult"] = authResult;

        // Log successful authentication
        _logger.LogInformation(
            "Authenticated user {UserId} for {Path}",
            authResult.UserId,
            context.Request.Path
        );

        // Continue to the next middleware
        await _next(context);
    }

    private bool IsSecuredEndpoint(PathString path)
    {
        // Define which endpoints require authentication
        return path.StartsWithSegments("/sse") || path.StartsWithSegments("/messages");
    }
}
