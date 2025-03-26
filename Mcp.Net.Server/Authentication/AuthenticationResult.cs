namespace Mcp.Net.Server.Authentication;

/// <summary>
/// Result of an authentication attempt
/// </summary>
public class AuthenticationResult
{
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
    public Dictionary<string, string> Claims { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Reason for failure, if authentication was not successful
    /// </summary>
    public string? FailureReason { get; set; }
}
