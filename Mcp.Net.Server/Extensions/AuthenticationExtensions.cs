using Mcp.Net.Server.Authentication;
using Mcp.Net.Server.Options;
using Mcp.Net.Server.ServerBuilder;
using Mcp.Net.Server.ServerBuilder.Helpers;
using Microsoft.Extensions.Options;

namespace Mcp.Net.Server.Extensions;

/// <summary>
/// Extension methods for integrating the MCP server authentication with ASP.NET Core applications.
/// </summary>
public static class AuthenticationExtensions
{
    /// <summary>
    /// Adds MCP authentication services to the service collection with options configuration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Options configuration delegate</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMcpAuthentication(
        this IServiceCollection services,
        Action<ServerAuthOptions> configureOptions
    )
    {
        // Register options
        services.Configure(configureOptions);

        // Register the auth handler factory
        services.AddSingleton<IAuthHandler>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ServerAuthOptions>>().Value;
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("Authentication");

            if (options == null || !options.Enabled)
            {
                logger.LogWarning("Authentication is disabled. All requests will be allowed.");
                return new NoAuthenticationHandler(new AuthOptions { Enabled = false });
            }

            // Check if we're using API key authentication
            if (options.ApiKeyOptions != null)
            {
                logger.LogInformation("Using API key authentication");
                var validator = sp.GetService<IApiKeyValidator>();

                if (validator == null)
                {
                    if (options.ApiKeyOptions.DevelopmentApiKey != null)
                    {
                        logger.LogWarning(
                            "Using development API key authentication. This should not be used in production."
                        );

                        validator = new InMemoryApiKeyValidator(
                            new Dictionary<string, string>
                            {
                                { options.ApiKeyOptions.DevelopmentApiKey, "Development" },
                            }
                        );
                    }
                    else
                    {
                        logger.LogWarning(
                            "No API key validator was registered. Authentication will fail for all requests."
                        );

                        // Create an empty validator as a fallback
                        validator = new InMemoryApiKeyValidator(new Dictionary<string, string>());
                    }
                }

                return new ApiKeyAuthenticationHandler(
                    options.ApiKeyOptions,
                    validator,
                    loggerFactory.CreateLogger<ApiKeyAuthenticationHandler>()
                );
            }

            // Default to no authentication if no specific handler is configured
            logger.LogWarning(
                "No authentication handler configured. All requests will be allowed."
            );
            return new NoAuthenticationHandler(new AuthOptions { Enabled = false });
        });

        return services;
    }

    /// <summary>
    /// Adds MCP authentication services to the service collection using options.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="options">The authentication options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMcpAuthentication(
        this IServiceCollection services,
        ServerAuthOptions options
    )
    {
        return services.AddMcpAuthentication(opt =>
        {
            opt.Enabled = options.Enabled;
            opt.ApiKeyOptions = options.ApiKeyOptions;
            opt.SecuredPaths =
                options.SecuredPaths != null ? new List<string>(options.SecuredPaths) : null;
        });
    }

    /// <summary>
    /// Adds MCP authentication services to the service collection with no authentication.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMcpAuthenticationNone(this IServiceCollection services)
    {
        // Register a no-auth handler
        services.AddSingleton<IAuthHandler>(
            new NoAuthenticationHandler(new AuthOptions { Enabled = false })
        );

        // Register the options to indicate no auth is configured
        services.Configure<ServerAuthOptions>(opt =>
        {
            opt.Enabled = false;
        });

        return services;
    }

    /// <summary>
    /// Adds MCP authentication services to the service collection.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="builder">The MCP server builder</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMcpAuthentication(
        this IServiceCollection services,
        McpServerBuilder builder
    )
    {
        // Register authentication services from the builder
        if (builder.AuthHandler != null)
        {
            services.AddSingleton<IAuthHandler>(builder.AuthHandler);

            // Get any AuthOptions from the transport builder
            var authOptions = AuthConfigurationHelpers.GetAuthOptionsFromBuilder(builder);
            if (authOptions != null)
            {
                services.AddSingleton(authOptions);
            }

            // Add server auth options based on the handler type
            if (builder.AuthHandler is ApiKeyAuthenticationHandler apiKeyHandler)
            {
                services.Configure<ServerAuthOptions>(opt =>
                {
                    opt.Enabled = true;
                    opt.ApiKeyOptions = apiKeyHandler.Options;
                    opt.SecuredPaths = apiKeyHandler.Options.SecuredPaths;
                });
            }
            else if (builder.AuthHandler is NoAuthenticationHandler)
            {
                services.Configure<ServerAuthOptions>(opt =>
                {
                    opt.Enabled = false;
                });
            }
        }

        if (builder.ApiKeyValidator != null)
        {
            services.AddSingleton<IApiKeyValidator>(builder.ApiKeyValidator);
        }

        return services;
    }
}