using Microsoft.AspNetCore.Http;

namespace Mcp.Net.Server.Authentication;

/// <summary>
/// Interface for authentication mechanisms
/// </summary>
public interface IAuthentication
{
    /// <summary>
    /// Authenticates an HTTP request
    /// </summary>
    /// <param name="context">The HTTP context of the request</param>
    /// <returns>Result of the authentication attempt</returns>
    Task<AuthenticationResult> AuthenticateAsync(HttpContext context);
}
