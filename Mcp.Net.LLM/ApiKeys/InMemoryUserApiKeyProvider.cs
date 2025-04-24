using System.Collections.Concurrent;
using Mcp.Net.LLM.Interfaces;
using Mcp.Net.LLM.Models;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.LLM.ApiKeys;

/// <summary>
/// In-memory implementation of IUserApiKeyProvider that stores API keys in memory
/// </summary>
public class InMemoryUserApiKeyProvider : IUserApiKeyProvider
{
    private readonly ILogger<InMemoryUserApiKeyProvider> _logger;
    private readonly IApiKeyProvider _fallbackProvider;

    // Dictionary structure: Provider -> UserId -> ApiKey
    private readonly ConcurrentDictionary<
        LlmProvider,
        ConcurrentDictionary<string, string>
    > _apiKeys = new();

    /// <summary>
    /// Initializes a new instance of InMemoryUserApiKeyProvider
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="fallbackProvider">The fallback API key provider to use when no user-specific key is found</param>
    public InMemoryUserApiKeyProvider(
        ILogger<InMemoryUserApiKeyProvider> logger,
        IApiKeyProvider fallbackProvider
    )
    {
        _logger = logger;
        _fallbackProvider = fallbackProvider;

        // Initialize dictionaries for each provider
        foreach (LlmProvider provider in Enum.GetValues(typeof(LlmProvider)))
        {
            _apiKeys[provider] = new ConcurrentDictionary<string, string>();
        }
    }

    /// <inheritdoc/>
    public async Task<string> GetApiKeyForUserAsync(LlmProvider provider, string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
        }

        // Check if we have a specific key for this user and provider
        if (
            _apiKeys.TryGetValue(provider, out var userKeys)
            && userKeys.TryGetValue(userId, out var apiKey)
            && !string.IsNullOrEmpty(apiKey)
        )
        {
            _logger.LogDebug(
                "Found user-specific API key for provider: {Provider}, user: {UserId}",
                provider,
                userId
            );
            return apiKey;
        }

        // Fall back to default provider if no user-specific key is found
        _logger.LogDebug(
            "No user-specific API key found, falling back to default for provider: {Provider}, user: {UserId}",
            provider,
            userId
        );
        try
        {
            return await _fallbackProvider.GetApiKeyAsync(provider);
        }
        catch (KeyNotFoundException)
        {
            _logger.LogWarning(
                "No API key found for provider: {Provider}, user: {UserId}",
                provider,
                userId
            );
            throw new KeyNotFoundException(
                $"No API key found for provider: {provider} and user: {userId}"
            );
        }
    }

    /// <inheritdoc/>
    public Task<bool> SetApiKeyForUserAsync(LlmProvider provider, string userId, string apiKey)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));
        }

        var success = _apiKeys[provider].TryAdd(userId, apiKey);

        if (!success)
        {
            // Key already exists, update it
            _apiKeys[provider][userId] = apiKey;
            success = true;
        }

        _logger.LogInformation(
            "Set API key for provider: {Provider}, user: {UserId}",
            provider,
            userId
        );
        return Task.FromResult(success);
    }

    /// <inheritdoc/>
    public Task<bool> HasApiKeyForUserAsync(LlmProvider provider, string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
        }

        var hasKey =
            _apiKeys.TryGetValue(provider, out var userKeys)
            && userKeys.TryGetValue(userId, out var apiKey)
            && !string.IsNullOrEmpty(apiKey);

        return Task.FromResult(hasKey);
    }

    /// <inheritdoc/>
    public Task<bool> DeleteApiKeyForUserAsync(LlmProvider provider, string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));
        }

        var success =
            _apiKeys.TryGetValue(provider, out var userKeys) && userKeys.TryRemove(userId, out _);

        if (success)
        {
            _logger.LogInformation(
                "Deleted API key for provider: {Provider}, user: {UserId}",
                provider,
                userId
            );
        }
        else
        {
            _logger.LogWarning(
                "Failed to delete API key for provider: {Provider}, user: {UserId}",
                provider,
                userId
            );
        }

        return Task.FromResult(success);
    }
}
