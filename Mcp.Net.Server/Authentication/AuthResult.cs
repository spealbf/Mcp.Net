using System.Security.Claims;

namespace Mcp.Net.Server.Authentication;

/// <summary>
/// Result of an authentication attempt
/// </summary>
/// <remarks>
/// This class encapsulates the result of an authentication operation,
/// including success/failure status, user identity information, and any claims.
/// It's designed to be extensible for different authentication schemes.
/// </remarks>
public class AuthResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthResult"/> class.
    /// </summary>
    public AuthResult()
    {
        Claims = new Dictionary<string, string>();
    }

    /// <summary>
    /// Creates a successful authentication result
    /// </summary>
    /// <param name="userId">The authenticated user ID</param>
    /// <param name="claims">Optional claims for the user</param>
    /// <returns>A successful authentication result</returns>
    public static AuthResult Success(string userId, Dictionary<string, string>? claims = null)
    {
        return new AuthResult
        {
            Succeeded = true,
            UserId = userId,
            Claims = claims ?? new Dictionary<string, string>(),
        };
    }

    /// <summary>
    /// Creates a failed authentication result
    /// </summary>
    /// <param name="reason">The reason for the failure</param>
    /// <returns>A failed authentication result</returns>
    public static AuthResult Fail(string reason)
    {
        return new AuthResult { Succeeded = false, FailureReason = reason };
    }

    /// <summary>
    /// Whether the authentication was successful
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// User ID for the authenticated user, if successful
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Additional claims for the authenticated user
    /// </summary>
    public Dictionary<string, string> Claims { get; set; }

    /// <summary>
    /// Reason for failure, if authentication was not successful
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// Converts this auth result to a ClaimsPrincipal
    /// </summary>
    /// <returns>A ClaimsPrincipal representing this authentication result</returns>
    public ClaimsPrincipal? ToClaimsPrincipal()
    {
        if (!Succeeded || string.IsNullOrEmpty(UserId))
        {
            return null;
        }

        var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, UserId) };

        // Add additional claims
        foreach (var claim in Claims)
        {
            claims.Add(new Claim(claim.Key, claim.Value));
        }

        var identity = new ClaimsIdentity(claims, "McpAuth");
        return new ClaimsPrincipal(identity);
    }
}
