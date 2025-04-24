using Mcp.Net.LLM.Models;

namespace Mcp.Net.LLM.Interfaces;

/// <summary>
/// Interface for providing user-specific API keys for different LLM providers
/// </summary>
public interface IUserApiKeyProvider
{
    /// <summary>
    /// Gets the API key for the specified provider and user
    /// </summary>
    /// <param name="provider">The LLM provider</param>
    /// <param name="userId">The ID of the user</param>
    /// <returns>The API key as a string</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no API key is available for the provider/user combination</exception>
    Task<string> GetApiKeyForUserAsync(LlmProvider provider, string userId);

    /// <summary>
    /// Sets the API key for the specified provider and user
    /// </summary>
    /// <param name="provider">The LLM provider</param>
    /// <param name="userId">The ID of the user</param>
    /// <param name="apiKey">The API key to store</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> SetApiKeyForUserAsync(LlmProvider provider, string userId, string apiKey);

    /// <summary>
    /// Checks if an API key exists for the specified provider and user
    /// </summary>
    /// <param name="provider">The LLM provider</param>
    /// <param name="userId">The ID of the user</param>
    /// <returns>True if an API key exists, false otherwise</returns>
    Task<bool> HasApiKeyForUserAsync(LlmProvider provider, string userId);

    /// <summary>
    /// Deletes the API key for the specified provider and user
    /// </summary>
    /// <param name="provider">The LLM provider</param>
    /// <param name="userId">The ID of the user</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> DeleteApiKeyForUserAsync(LlmProvider provider, string userId);
}
