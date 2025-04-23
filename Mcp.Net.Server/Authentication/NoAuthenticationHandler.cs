using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Mcp.Net.Server.Authentication;

/// <summary>
/// Authentication handler that always succeeds, effectively disabling authentication.
/// </summary>
public class NoAuthenticationHandler : IAuthHandler
{
    private readonly AuthOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="NoAuthenticationHandler"/> class.
    /// </summary>
    /// <param name="options">The authentication options.</param>
    public NoAuthenticationHandler(AuthOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Enabled = false;
    }

    /// <summary>
    /// Gets the authentication scheme name.
    /// </summary>
    public string SchemeName => "NoAuth";

    /// <summary>
    /// Gets the authentication options.
    /// </summary>
    public AuthOptions Options => _options;

    /// <summary>
    /// Authenticates a request, always returns a successful result.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A successful authentication result.</returns>
    public Task<AuthResult> AuthenticateAsync(HttpContext context)
    {
        // Create a dictionary of claims
        var claims = new Dictionary<string, string> { { ClaimTypes.Name, "Anonymous" } };

        // Return a successful result
        var result = AuthResult.Success("anonymous", claims);
        return Task.FromResult(result);
    }

    /// <summary>
    /// Checks if a path is secured and requires authentication.
    /// </summary>
    /// <param name="path">The request path.</param>
    /// <returns>False, since no paths are secured.</returns>
    public bool IsSecuredPath(string path)
    {
        // Nothing is secured with this handler
        return false;
    }
}
