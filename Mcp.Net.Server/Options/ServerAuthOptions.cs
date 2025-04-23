using Mcp.Net.Server.Authentication;

namespace Mcp.Net.Server.Options;

/// <summary>
/// Represents options for configuring authentication in an MCP server.
/// </summary>
public class ServerAuthOptions
{
    /// <summary>
    /// Gets or sets whether authentication is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether authentication is explicitly disabled.
    /// </summary>
    public bool NoAuthExplicitlyConfigured { get; set; } = false;

    /// <summary>
    /// Gets or sets the authentication handler.
    /// </summary>
    public IAuthHandler? AuthHandler { get; set; }

    /// <summary>
    /// Gets or sets the API key validator.
    /// </summary>
    public IApiKeyValidator? ApiKeyValidator { get; set; }

    /// <summary>
    /// Gets or sets the authentication header name.
    /// </summary>
    public string HeaderName { get; set; } = "X-API-Key";

    /// <summary>
    /// Gets or sets the authentication query parameter name.
    /// </summary>
    public string QueryParamName { get; set; } = "api_key";

    /// <summary>
    /// Gets or sets the API key authentication options.
    /// </summary>
    public ApiKeyAuthOptions? ApiKeyOptions { get; set; }

    /// <summary>
    /// Gets or sets the secured paths that require authentication.
    /// </summary>
    public List<string>? SecuredPaths { get; set; }

    /// <summary>
    /// Gets or sets the development API key.
    /// </summary>
    /// <remarks>
    /// This is for development/testing only. Do not use in production.
    /// </remarks>
    public string? DevelopmentApiKey { get; set; }

    /// <summary>
    /// Gets or sets additional API keys.
    /// </summary>
    public Dictionary<string, string> ApiKeys { get; set; } = new();

    /// <summary>
    /// Gets whether security is configured.
    /// </summary>
    public bool IsSecurityConfigured =>
        NoAuthExplicitlyConfigured
        || AuthHandler != null
        || ApiKeyValidator != null
        || !string.IsNullOrEmpty(DevelopmentApiKey)
        || ApiKeys.Count > 0;

    /// <summary>
    /// Creates API key options from the current configuration.
    /// </summary>
    /// <returns>Configured API key options</returns>
    public ApiKeyAuthOptions ToApiKeyOptions()
    {
        return new ApiKeyAuthOptions
        {
            HeaderName = HeaderName,
            QueryParamName = QueryParamName,
            DevelopmentApiKey = DevelopmentApiKey,
        };
    }

    /// <summary>
    /// Configures the options with a specific development API key.
    /// </summary>
    /// <param name="apiKey">The development API key</param>
    /// <returns>The options instance for chaining</returns>
    public ServerAuthOptions WithApiKey(string apiKey)
    {
        DevelopmentApiKey = apiKey;
        return this;
    }

    /// <summary>
    /// Configures the options with multiple API keys.
    /// </summary>
    /// <param name="apiKeys">Dictionary mapping API keys to user IDs</param>
    /// <returns>The options instance for chaining</returns>
    public ServerAuthOptions WithApiKeys(Dictionary<string, string> apiKeys)
    {
        ApiKeys = apiKeys;
        return this;
    }

    /// <summary>
    /// Disables authentication.
    /// </summary>
    /// <returns>The options instance for chaining</returns>
    public ServerAuthOptions WithNoAuth()
    {
        NoAuthExplicitlyConfigured = true;
        return this;
    }

    /// <summary>
    /// Configures the options with a custom authentication handler.
    /// </summary>
    /// <param name="authHandler">The authentication handler</param>
    /// <returns>The options instance for chaining</returns>
    public ServerAuthOptions WithAuthentication(IAuthHandler authHandler)
    {
        AuthHandler = authHandler;
        return this;
    }

    /// <summary>
    /// Configures the options with a custom API key validator.
    /// </summary>
    /// <param name="validator">The API key validator</param>
    /// <returns>The options instance for chaining</returns>
    public ServerAuthOptions WithApiKeyValidator(IApiKeyValidator validator)
    {
        ApiKeyValidator = validator;
        return this;
    }
}
