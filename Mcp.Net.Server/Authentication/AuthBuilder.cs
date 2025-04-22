using Microsoft.Extensions.Logging;

namespace Mcp.Net.Server.Authentication;

/// <summary>
/// Builder for configuring authentication for MCP servers
/// </summary>
/// <remarks>
/// This builder provides a fluent API for configuring authentication.
/// It can create authentication handlers for different schemes.
/// </remarks>
public class AuthBuilder
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<AuthBuilder> _logger;
    private AuthOptions _options = new();
    private IAuthHandler? _authHandler;
    private IApiKeyValidator? _apiKeyValidator;
    private ApiKeyAuthOptions? _apiKeyOptions;
    private bool _authDisabled;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthBuilder"/> class.
    /// </summary>
    /// <param name="loggerFactory">Logger factory for creating loggers</param>
    public AuthBuilder(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = _loggerFactory.CreateLogger<AuthBuilder>();
    }

    /// <summary>
    /// Gets the authentication handler that's been configured
    /// </summary>
    public IAuthHandler? AuthHandler => _authHandler;

    /// <summary>
    /// Gets the API key validator that's been configured
    /// </summary>
    public IApiKeyValidator? ApiKeyValidator => _apiKeyValidator;

    /// <summary>
    /// Gets the API key options that have been configured
    /// </summary>
    public ApiKeyAuthOptions? ApiKeyOptions => _apiKeyOptions;

    /// <summary>
    /// Gets whether authentication has been disabled
    /// </summary>
    public bool IsAuthDisabled => _authDisabled;

    /// <summary>
    /// Configures common authentication options
    /// </summary>
    /// <param name="configure">Action to configure options</param>
    /// <returns>The builder for chaining</returns>
    public AuthBuilder WithOptions(Action<AuthOptions> configure)
    {
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        configure(_options);
        return this;
    }

    /// <summary>
    /// Configures paths that require authentication
    /// </summary>
    /// <param name="paths">The paths that require authentication</param>
    /// <returns>The builder for chaining</returns>
    public AuthBuilder WithSecuredPaths(params string[] paths)
    {
        if (paths == null || paths.Length == 0)
            throw new ArgumentException("At least one path must be specified", nameof(paths));

        _options.SecuredPaths = new List<string>(paths);
        return this;
    }

    /// <summary>
    /// Disables authentication entirely
    /// </summary>
    /// <returns>The builder for chaining</returns>
    public AuthBuilder WithNoAuth()
    {
        _authDisabled = true;
        _options.Enabled = false;
        _logger.LogWarning(
            "Authentication has been disabled. This is not recommended for production environments."
        );
        return this;
    }

    /// <summary>
    /// Configures a custom authentication handler
    /// </summary>
    /// <param name="authHandler">The custom authentication handler</param>
    /// <returns>The builder for chaining</returns>
    public AuthBuilder WithHandler(IAuthHandler authHandler)
    {
        _authHandler = authHandler ?? throw new ArgumentNullException(nameof(authHandler));
        _logger.LogInformation(
            "Using custom authentication handler: {Handler}",
            authHandler.GetType().Name
        );
        return this;
    }

    /// <summary>
    /// Configures API key authentication with a single key
    /// </summary>
    /// <param name="apiKey">The API key to use</param>
    /// <param name="userId">The user ID to associate with the key</param>
    /// <param name="claims">Optional claims for the user</param>
    /// <returns>The builder for chaining</returns>
    public AuthBuilder WithApiKey(
        string apiKey,
        string userId,
        Dictionary<string, string>? claims = null
    )
    {
        if (string.IsNullOrEmpty(apiKey))
            throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));

        if (string.IsNullOrEmpty(userId))
            throw new ArgumentException("User ID cannot be null or empty", nameof(userId));

        // Create validator if needed
        if (_apiKeyValidator == null)
        {
            _apiKeyValidator = new InMemoryApiKeyValidator(
                _loggerFactory.CreateLogger<InMemoryApiKeyValidator>()
            );

            // Create default options if needed
            if (_apiKeyOptions == null)
            {
                _apiKeyOptions = new ApiKeyAuthOptions();
            }
        }

        // Add the key
        ((InMemoryApiKeyValidator)_apiKeyValidator).AddApiKey(apiKey, userId, claims);

        _logger.LogInformation("Added API key for user {UserId}", userId);
        return this;
    }

    /// <summary>
    /// Configures API key authentication with multiple keys
    /// </summary>
    /// <param name="apiKeys">The API keys to register</param>
    /// <returns>The builder for chaining</returns>
    public AuthBuilder WithApiKeys(Dictionary<string, string> apiKeys)
    {
        if (apiKeys == null || !apiKeys.Any())
            throw new ArgumentException(
                "API keys dictionary cannot be null or empty",
                nameof(apiKeys)
            );

        // Create validator if needed
        if (_apiKeyValidator == null)
        {
            _apiKeyValidator = new InMemoryApiKeyValidator(
                _loggerFactory.CreateLogger<InMemoryApiKeyValidator>()
            );

            // Create default options if needed
            if (_apiKeyOptions == null)
            {
                _apiKeyOptions = new ApiKeyAuthOptions();
            }
        }

        // Add all keys
        foreach (var (key, userId) in apiKeys)
        {
            ((InMemoryApiKeyValidator)_apiKeyValidator).AddApiKey(key, userId);
        }

        _logger.LogInformation("Added {Count} API keys", apiKeys.Count);
        return this;
    }

    /// <summary>
    /// Configures API key authentication with a custom validator
    /// </summary>
    /// <param name="validator">The custom validator to use</param>
    /// <returns>The builder for chaining</returns>
    public AuthBuilder WithApiKeyValidator(IApiKeyValidator validator)
    {
        _apiKeyValidator = validator ?? throw new ArgumentNullException(nameof(validator));

        // Create default options if needed
        if (_apiKeyOptions == null)
        {
            _apiKeyOptions = new ApiKeyAuthOptions();
        }

        _logger.LogInformation(
            "Using custom API key validator: {Validator}",
            validator.GetType().Name
        );
        return this;
    }

    /// <summary>
    /// Configures options for API key authentication
    /// </summary>
    /// <param name="configure">Action to configure options</param>
    /// <returns>The builder for chaining</returns>
    public AuthBuilder WithApiKeyOptions(Action<ApiKeyAuthOptions> configure)
    {
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        // Create options if needed
        if (_apiKeyOptions == null)
        {
            _apiKeyOptions = new ApiKeyAuthOptions();
        }

        // Apply configuration
        configure(_apiKeyOptions);

        // If a development API key was specified, register it
        if (!string.IsNullOrEmpty(_apiKeyOptions.DevelopmentApiKey))
        {
            // Create validator if needed
            if (_apiKeyValidator == null)
            {
                _apiKeyValidator = new InMemoryApiKeyValidator(
                    _loggerFactory.CreateLogger<InMemoryApiKeyValidator>()
                );
            }

            // Check if we might be in production (by looking for common production environment variables)
            bool mightBeProduction = 
                !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")) && 
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")?.Equals("Production", StringComparison.OrdinalIgnoreCase) == true;

            if (mightBeProduction)
            {
                _logger.LogWarning(
                    "SECURITY WARNING: Development API key is being used in what appears to be a production environment. " +
                    "This is a security risk and should be avoided. Use explicit API key registration instead."
                );
            }

            // Add the development key
            if (_apiKeyValidator is InMemoryApiKeyValidator inMemoryValidator)
            {
                inMemoryValidator.AddApiKey(_apiKeyOptions.DevelopmentApiKey, "dev-user");
                _logger.LogWarning(
                    "Added development API key for user 'dev-user'. " +
                    "This feature is intended for development/testing only. "
                );
            }
        }

        return this;
    }

    /// <summary>
    /// Builds the configured authentication handler
    /// </summary>
    /// <returns>The configured authentication handler, or null if authentication is disabled</returns>
    public IAuthHandler? Build()
    {
        // If auth is disabled, return null
        if (_authDisabled)
        {
            _logger.LogWarning("Authentication is disabled");
            return null;
        }

        // If an auth handler was already configured, use it
        if (_authHandler != null)
        {
            return _authHandler;
        }

        // If API key auth was configured, create a handler
        if (_apiKeyValidator != null)
        {
            var options = _apiKeyOptions ?? new ApiKeyAuthOptions();

            // Apply base options
            options.Enabled = _options.Enabled;
            options.SecuredPaths = _options.SecuredPaths;
            options.EnableLogging = _options.EnableLogging;

            // Create the handler
            var handler = new ApiKeyAuthenticationHandler(
                options,
                _apiKeyValidator,
                _loggerFactory.CreateLogger<ApiKeyAuthenticationHandler>()
            );

            _logger.LogInformation("Created API key authentication handler");
            return handler;
        }

        // No authentication was configured
        _logger.LogWarning("No authentication configured, returning null handler");
        return null;
    }
}
