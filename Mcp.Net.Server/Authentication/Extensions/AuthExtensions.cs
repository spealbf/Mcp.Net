using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.Server.Authentication.Extensions;

/// <summary>
/// Extension methods for configuring authentication services for MCP servers
/// using ASP.NET Core dependency injection
/// </summary>
/// <remarks>
/// These extensions follow standard ASP.NET Core patterns for registering
/// and configuring authentication services.
/// </remarks>
public static class AuthExtensions
{
    /// <summary>
    /// Adds MCP authentication services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Action to configure authentication options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMcpAuthentication(
        this IServiceCollection services,
        Action<AuthBuilder> configure
    )
    {
        // Get or create a logger factory
        var loggerFactory =
            services.BuildServiceProvider().GetService<ILoggerFactory>() ?? new LoggerFactory();

        // Create and configure the auth builder
        var authBuilder = new AuthBuilder(loggerFactory);
        configure(authBuilder);

        // Build the auth handler
        var authHandler = authBuilder.Build();

        // Register authentication options
        services.AddSingleton(
            new AuthOptions
            {
                Enabled = !authBuilder.IsAuthDisabled,
                SecuredPaths = authBuilder.IsAuthDisabled
                    ? new List<string>()
                    : (authBuilder.ApiKeyOptions?.SecuredPaths ?? new List<string>()),
                EnableLogging = true,
            }
        );

        // Register API key validation if configured
        if (authBuilder.ApiKeyValidator != null)
        {
            services.AddSingleton<IApiKeyValidator>(authBuilder.ApiKeyValidator);

            // Register API key options if configured
            if (authBuilder.ApiKeyOptions != null)
            {
                services.AddSingleton(authBuilder.ApiKeyOptions);
            }
        }

        // Register auth handler if created
        if (authHandler != null)
        {
            services.AddSingleton<IAuthHandler>(authHandler);
        }

        return services;
    }

    /// <summary>
    /// Adds API key authentication to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Action to configure API key authentication</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddApiKeyAuthentication(
        this IServiceCollection services,
        Action<ApiKeyAuthConfigurer> configure
    )
    {
        // Create a configurer that will set up API key authentication
        var configurer = new ApiKeyAuthConfigurer(services);
        configure(configurer);

        return services;
    }

    /// <summary>
    /// Configures authentication to be disabled
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddNoAuthentication(this IServiceCollection services)
    {
        // Register auth options with authentication disabled
        services.AddSingleton(
            new AuthOptions
            {
                Enabled = false,
                SecuredPaths = new List<string>(),
                EnableLogging = true,
            }
        );

        return services;
    }
}

/// <summary>
/// Configurer for API key authentication
/// </summary>
/// <remarks>
/// This class provides a more focused API for configuring API key authentication
/// specifically, following ASP.NET Core patterns.
/// </remarks>
public class ApiKeyAuthConfigurer
{
    private readonly IServiceCollection _services;
    private readonly ApiKeyAuthOptions _options = new();
    private InMemoryApiKeyValidator? _validator;
    private IApiKeyValidator? _customValidator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiKeyAuthConfigurer"/> class
    /// </summary>
    /// <param name="services">The service collection</param>
    public ApiKeyAuthConfigurer(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Configures options for API key authentication
    /// </summary>
    /// <param name="configure">Action to configure options</param>
    /// <returns>The configurer for chaining</returns>
    public ApiKeyAuthConfigurer ConfigureOptions(Action<ApiKeyAuthOptions> configure)
    {
        configure(_options);
        return this;
    }

    /// <summary>
    /// Sets paths that require authentication
    /// </summary>
    /// <param name="paths">The paths to secure</param>
    /// <returns>The configurer for chaining</returns>
    public ApiKeyAuthConfigurer AddSecuredPaths(params string[] paths)
    {
        if (_options.SecuredPaths == null)
        {
            _options.SecuredPaths = new List<string>();
        }

        foreach (var path in paths)
        {
            _options.SecuredPaths.Add(path);
        }

        return this;
    }

    /// <summary>
    /// Adds a single API key
    /// </summary>
    /// <param name="apiKey">The API key</param>
    /// <param name="userId">The user ID associated with the key</param>
    /// <param name="claims">Optional claims for the user</param>
    /// <returns>The configurer for chaining</returns>
    public ApiKeyAuthConfigurer AddApiKey(
        string apiKey,
        string userId,
        Dictionary<string, string>? claims = null
    )
    {
        if (_customValidator != null)
        {
            throw new InvalidOperationException(
                "Cannot add API keys when a custom validator has been set"
            );
        }

        // Create a validator if needed
        if (_validator == null)
        {
            var loggerFactory =
                _services.BuildServiceProvider().GetService<ILoggerFactory>()
                ?? new LoggerFactory();

            _validator = new InMemoryApiKeyValidator(
                loggerFactory.CreateLogger<InMemoryApiKeyValidator>()
            );
        }

        // Add the key
        _validator.AddApiKey(apiKey, userId, claims);

        return this;
    }

    /// <summary>
    /// Adds multiple API keys
    /// </summary>
    /// <param name="apiKeys">Dictionary mapping API keys to user IDs</param>
    /// <returns>The configurer for chaining</returns>
    public ApiKeyAuthConfigurer AddApiKeys(Dictionary<string, string> apiKeys)
    {
        if (_customValidator != null)
        {
            throw new InvalidOperationException(
                "Cannot add API keys when a custom validator has been set"
            );
        }

        // Create a validator if needed
        if (_validator == null)
        {
            var loggerFactory =
                _services.BuildServiceProvider().GetService<ILoggerFactory>()
                ?? new LoggerFactory();

            _validator = new InMemoryApiKeyValidator(
                loggerFactory.CreateLogger<InMemoryApiKeyValidator>()
            );
        }

        // Add all keys
        foreach (var (key, userId) in apiKeys)
        {
            _validator.AddApiKey(key, userId);
        }

        return this;
    }

    /// <summary>
    /// Sets a custom API key validator
    /// </summary>
    /// <param name="validator">The validator to use</param>
    /// <returns>The configurer for chaining</returns>
    public ApiKeyAuthConfigurer UseValidator(IApiKeyValidator validator)
    {
        if (_validator != null)
        {
            throw new InvalidOperationException(
                "Cannot set a custom validator when API keys have already been added"
            );
        }

        _customValidator = validator;
        return this;
    }

    /// <summary>
    /// Builds and registers all services for API key authentication
    /// </summary>
    internal void ConfigureServices()
    {
        // Register the API key options
        _services.AddSingleton(_options);

        // Register the auth options
        _services.AddSingleton(
            new AuthOptions
            {
                Enabled = true,
                SecuredPaths = _options.SecuredPaths,
                EnableLogging = true,
            }
        );

        // Register the validator
        var validator = _customValidator ?? _validator;
        if (validator != null)
        {
            _services.AddSingleton<IApiKeyValidator>(validator);
        }
        else if (!string.IsNullOrEmpty(_options.DevelopmentApiKey))
        {
            // Create a validator with the development API key
            var loggerFactory =
                _services.BuildServiceProvider().GetService<ILoggerFactory>()
                ?? new LoggerFactory();

            var logger = loggerFactory.CreateLogger<ApiKeyAuthConfigurer>();

            // Check if we might be in production
            bool mightBeProduction = 
                !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")) && 
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")?.Equals("Production", StringComparison.OrdinalIgnoreCase) == true;

            if (mightBeProduction)
            {
                logger.LogWarning(
                    "SECURITY WARNING: Development API key is being used in what appears to be a production environment. " +
                    "This is a security risk and should be avoided. Use explicit API key registration instead."
                );
            }

            var devValidator = new InMemoryApiKeyValidator(
                loggerFactory.CreateLogger<InMemoryApiKeyValidator>()
            );

            devValidator.AddApiKey(_options.DevelopmentApiKey, "dev-user");
            logger.LogWarning(
                "Added development API key for user 'dev-user'. " +
                "This feature is intended for development/testing only."
            );
            _services.AddSingleton<IApiKeyValidator>(devValidator);
        }

        // Register the auth handler
        _services.AddSingleton<IAuthHandler>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var validator = sp.GetRequiredService<IApiKeyValidator>();
            var options = sp.GetRequiredService<ApiKeyAuthOptions>();

            return new ApiKeyAuthenticationHandler(
                options,
                validator,
                loggerFactory.CreateLogger<ApiKeyAuthenticationHandler>()
            );
        });
    }
}
