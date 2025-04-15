using Microsoft.AspNetCore.Http;

namespace Mcp.Net.Server.Authentication;

/// <summary>
/// Interface for authentication handlers that validate requests
/// </summary>
/// <remarks>
/// This interface provides a clean abstraction for authentication handlers.
/// It's designed to be extensible for different authentication schemes
/// while maintaining a simple core contract.
/// </remarks>
public interface IAuthHandler
{
    /// <summary>
    /// Gets the name of the authentication scheme
    /// </summary>
    string SchemeName { get; }

    /// <summary>
    /// Authenticates an HTTP request
    /// </summary>
    /// <param name="context">The HTTP context of the request</param>
    /// <returns>Result of the authentication attempt</returns>
    Task<AuthResult> AuthenticateAsync(HttpContext context);
}