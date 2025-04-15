using System.Security.Claims;

namespace Mcp.Net.Server.Authentication;

/// <summary>
/// Middleware that handles authentication for MCP endpoints
/// </summary>
/// <remarks>
/// This middleware applies authentication to secured endpoints.
/// It uses the configured <see cref="IAuthHandler"/> to authenticate requests
/// and sets up the HTTP context with the authentication result.
/// </remarks>
public class McpAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAuthHandler? _authHandler;
    private readonly ILogger<McpAuthenticationMiddleware> _logger;
    private readonly AuthOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpAuthenticationMiddleware"/> class
    /// </summary>
    /// <param name="next">The next middleware in the pipeline</param>
    /// <param name="logger">Logger for authentication events</param>
    /// <param name="authHandler">Optional authentication handler</param>
    /// <param name="options">Optional authentication options</param>
    public McpAuthenticationMiddleware(
        RequestDelegate next,
        ILogger<McpAuthenticationMiddleware> logger,
        IAuthHandler? authHandler = null,
        AuthOptions? options = null
    )
    {
        _next = next;
        _authHandler = authHandler;
        _logger = logger;
        _options = options ?? new AuthOptions();
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
        if (!_options.Enabled || _authHandler == null)
        {
            if (_options.EnableLogging)
            {
                _logger.LogWarning(
                    "Authentication not configured for secured endpoint {Path}. "
                        + "This is potentially insecure. Configure authentication with WithAuthentication().",
                    context.Request.Path
                );
            }
            await _next(context);
            return;
        }

        // Authenticate the request
        var authResult = await _authHandler.AuthenticateAsync(context);
        if (!authResult.Succeeded)
        {
            if (_options.EnableLogging)
            {
                _logger.LogWarning(
                    "Authentication failed for {Path}: {Reason}",
                    context.Request.Path,
                    authResult.FailureReason
                );
            }

            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(
                new { error = "Unauthorized", message = authResult.FailureReason }
            );
            return;
        }

        // Store auth result in context for later use
        context.Items["AuthResult"] = authResult;

        // Set up claims principal if available
        var principal = authResult.ToClaimsPrincipal();
        if (principal != null)
        {
            context.User = principal;
        }

        // Continue to the next middleware
        await _next(context);
    }

    private bool IsSecuredEndpoint(PathString path)
    {
        // Check each secured path pattern
        foreach (var securedPath in _options.SecuredPaths)
        {
            if (path.StartsWithSegments(securedPath))
            {
                return true;
            }
        }

        return false;
    }
}
