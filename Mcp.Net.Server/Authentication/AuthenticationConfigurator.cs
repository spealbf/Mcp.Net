using Microsoft.Extensions.Logging;

namespace Mcp.Net.Server.Authentication;

/// <summary>
/// Helper class for configuring authentication for MCP servers.
/// </summary>
public static class AuthenticationConfigurator
{
    /// <summary>
    /// Configures authentication based on available providers and validators.
    /// </summary>
    /// <param name="validator">Optional API key validator</param>
    /// <param name="explicitAuth">Optional explicit authentication provider</param>
    /// <param name="loggerFactory">Logger factory for creating authentication loggers</param>
    /// <returns>The configured authentication provider, or null if no authentication is configured</returns>
    public static IAuthentication? ConfigureAuthentication(
        IApiKeyValidator? validator,
        IAuthentication? explicitAuth,
        ILoggerFactory loggerFactory
    )
    {
        // If explicit authentication is provided, use it
        if (explicitAuth != null)
            return explicitAuth;

        // If a validator is provided, create an API key authentication handler
        if (validator != null)
        {
            var options = new ApiKeyAuthOptions
            {
                HeaderName = "X-API-Key",
                QueryParamName = "api_key",
            };

            return new ApiKeyAuthenticationHandler(
                options,
                validator,
                loggerFactory.CreateLogger<ApiKeyAuthenticationHandler>()
            );
        }

        // No authentication configured
        return null;
    }

    /// <summary>
    /// Creates an API key validator with predefined keys.
    /// </summary>
    /// <param name="loggerFactory">Logger factory for creating validator loggers</param>
    /// <param name="apiKey">Single API key to use</param>
    /// <returns>The configured API key validator</returns>
    public static IApiKeyValidator CreateApiKeyValidator(
        ILoggerFactory loggerFactory,
        string apiKey
    )
    {
        var logger = loggerFactory.CreateLogger<InMemoryApiKeyValidator>();
        var validator = new InMemoryApiKeyValidator(logger);

        // Add the provided API key for a default user
        validator.AddApiKey(apiKey, "default-user");

        return validator;
    }

    /// <summary>
    /// Creates an API key validator with multiple predefined keys.
    /// </summary>
    /// <param name="loggerFactory">Logger factory for creating validator loggers</param>
    /// <param name="apiKeys">Collection of API keys to use</param>
    /// <returns>The configured API key validator</returns>
    public static IApiKeyValidator CreateApiKeyValidator(
        ILoggerFactory loggerFactory,
        IEnumerable<string> apiKeys
    )
    {
        var logger = loggerFactory.CreateLogger<InMemoryApiKeyValidator>();
        var validator = new InMemoryApiKeyValidator(logger);

        // Add all the provided API keys for default users
        int i = 0;
        foreach (var key in apiKeys)
        {
            validator.AddApiKey(key, $"default-user-{++i}");
        }

        return validator;
    }
}
