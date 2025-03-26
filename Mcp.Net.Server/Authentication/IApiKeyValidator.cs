namespace Mcp.Net.Server.Authentication;

/// <summary>
/// Interface for API key validation
/// </summary>
public interface IApiKeyValidator
{
    /// <summary>
    /// Validates an API key
    /// </summary>
    /// <param name="apiKey">The API key to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    Task<bool> IsValidAsync(string apiKey);

    /// <summary>
    /// Gets the user ID associated with an API key
    /// </summary>
    /// <param name="apiKey">The API key</param>
    /// <returns>The user ID if valid, null otherwise</returns>
    Task<string?> GetUserIdAsync(string apiKey);

    /// <summary>
    /// Gets claims associated with an API key
    /// </summary>
    /// <param name="apiKey">The API key</param>
    /// <returns>Dictionary of claims if valid, null otherwise</returns>
    Task<Dictionary<string, string>?> GetClaimsAsync(string apiKey);
}
