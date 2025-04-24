using Mcp.Net.LLM.Models;

namespace Mcp.Net.LLM.Interfaces;

/// <summary>
/// Interface for providing API keys for different LLM providers
/// </summary>
public interface IApiKeyProvider
{
    /// <summary>
    /// Gets the API key for the specified provider
    /// </summary>
    /// <param name="provider">The LLM provider</param>
    /// <returns>The API key as a string</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no API key is available for the provider</exception>
    Task<string> GetApiKeyAsync(LlmProvider provider);
}
