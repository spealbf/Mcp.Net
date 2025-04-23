using Microsoft.Extensions.Logging;

namespace Mcp.Net.Server.Authentication;

/// <summary>
/// Simple in-memory implementation of API key validator
/// </summary>
public class InMemoryApiKeyValidator : IApiKeyValidator
{
    private readonly Dictionary<string, ApiKeyInfo> _apiKeys = new();
    private readonly ILogger<InMemoryApiKeyValidator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryApiKeyValidator"/> class
    /// </summary>
    /// <param name="logger">Logger for the validator</param>
    public InMemoryApiKeyValidator(ILogger<InMemoryApiKeyValidator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryApiKeyValidator"/> class
    /// with predefined API keys
    /// </summary>
    /// <param name="apiKeys">Dictionary mapping API keys to user IDs</param>
    public InMemoryApiKeyValidator(Dictionary<string, string> apiKeys)
    {
        _logger = new LoggerFactory().CreateLogger<InMemoryApiKeyValidator>();

        foreach (var apiKey in apiKeys)
        {
            AddApiKey(apiKey.Key, apiKey.Value);
        }
    }

    /// <summary>
    /// Adds or updates an API key
    /// </summary>
    /// <param name="apiKey">The API key</param>
    /// <param name="userId">User ID associated with the key</param>
    /// <param name="claims">Optional claims for the user</param>
    public void AddApiKey(string apiKey, string userId, Dictionary<string, string>? claims = null)
    {
        _apiKeys[apiKey] = new ApiKeyInfo
        {
            UserId = userId,
            Claims = claims ?? new Dictionary<string, string>(),
        };

        _logger.LogInformation("Added API key for user {UserId}", userId);
    }

    /// <inheritdoc/>
    public Task<bool> IsValidAsync(string apiKey)
    {
        return Task.FromResult(_apiKeys.ContainsKey(apiKey));
    }

    /// <inheritdoc/>
    public Task<string?> GetUserIdAsync(string apiKey)
    {
        if (_apiKeys.TryGetValue(apiKey, out var info))
        {
            return Task.FromResult<string?>(info.UserId);
        }
        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public Task<Dictionary<string, string>?> GetClaimsAsync(string apiKey)
    {
        if (_apiKeys.TryGetValue(apiKey, out var info))
        {
            return Task.FromResult<Dictionary<string, string>?>(
                new Dictionary<string, string>(info.Claims)
            );
        }
        return Task.FromResult<Dictionary<string, string>?>(null);
    }

    private class ApiKeyInfo
    {
        public required string UserId { get; init; }
        public Dictionary<string, string> Claims { get; init; } = new();
    }
}
