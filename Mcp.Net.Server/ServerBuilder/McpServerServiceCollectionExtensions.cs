using System.Reflection;
using Mcp.Net.Server.Authentication;
using Mcp.Net.Server.Extensions;
using Mcp.Net.Server.Logging;
using Mcp.Net.Server.Options;
using Mcp.Net.Server.Transport.Sse;
using Microsoft.Extensions.Options;

namespace Mcp.Net.Server.ServerBuilder;

/// <summary>
/// Extension methods for integrating the MCP server with ASP.NET Core applications.
/// </summary>
public static class McpServerServiceCollectionExtensions
{
    /// <summary>
    /// Adds an MCP server to the application middleware pipeline with default options.
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <returns>The application builder for chaining</returns>
    /// <remarks>
    /// This is a convenience method that uses the default middleware options.
    /// For more control, use the overload in <see cref="McpServerExtensions.UseMcpServer"/>.
    /// </remarks>
    public static IApplicationBuilder UseMcpServer(this IApplicationBuilder app)
    {
        // Use the configurable version from McpServerExtensions
        return Extensions.McpServerExtensions.UseMcpServer(app);
    }

    /// <summary>
    /// Adds MCP server services to the service collection.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Builder configuration delegate</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMcpServer(
        this IServiceCollection services,
        Action<McpServerBuilder> configure
    )
    {
        // Create and configure builder with SSE transport
        var builder = McpServerBuilder.ForSse();
        configure(builder);

        // Validate the transport builder
        if (builder.TransportBuilder is not SseServerBuilder sseBuilder)
        {
            throw new InvalidOperationException(
                "AddMcpServer requires an SSE transport. Use McpServerBuilder.ForSse() to create the builder."
            );
        }

        // Add MCP core services
        services.AddMcpCore(builder);

        // Add MCP logging services
        services.AddMcpLogging(builder);

        // Add MCP authentication services
        services.AddMcpAuthentication(builder);

        // Add MCP tool registration services
        services.AddMcpTools(builder);

        // Add CORS services if not already registered
        services.AddMcpCorsServices();

        // Add SSE transport services
        services.AddMcpSseTransport(sseBuilder);

        return services;
    }

    /// <summary>
    /// Adds MCP core services to the service collection with options configuration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Options configuration delegate</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMcpCore(
        this IServiceCollection services,
        Action<McpServerOptions> configureOptions
    )
    {
        // Register options
        services.Configure(configureOptions);

        // Register the McpServer singleton
        services.AddSingleton<McpServer>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("McpServerBuilder");
            var options = sp.GetRequiredService<IOptions<McpServerOptions>>().Value;

            logger.LogInformation(
                "Building McpServer instance with name: {ServerName}",
                options.Name
            );

            // Create a new builder and configure it from options
            var builder = McpServerBuilder
                .ForSse()
                .WithName(options.Name)
                .WithVersion(options.Version);

            if (options.Instructions != null)
            {
                builder.WithInstructions(options.Instructions);
            }

            // Add tool assemblies if specified
            foreach (var path in options.ToolAssemblyPaths)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(path);
                    builder.ScanToolsFromAssembly(assembly);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to load assembly from path: {Path}", path);
                }
            }

            var server = builder.Build();
            return server;
        });

        return services;
    }

    /// <summary>
    /// Adds MCP core services to the service collection using options.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="options">The server options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMcpCore(
        this IServiceCollection services,
        McpServerOptions options
    )
    {
        return services.AddMcpCore(opt =>
        {
            opt.Name = options.Name;
            opt.Version = options.Version;
            opt.Instructions = options.Instructions;
            opt.LogLevel = options.LogLevel;
            opt.UseConsoleLogging = options.UseConsoleLogging;
            opt.LogFilePath = options.LogFilePath;
            opt.NoAuthExplicitlyConfigured = options.NoAuthExplicitlyConfigured;
            opt.ToolAssemblyPaths = new List<string>(options.ToolAssemblyPaths);
            opt.Capabilities = options.Capabilities;
        });
    }

    /// <summary>
    /// Adds MCP core services to the service collection.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="builder">The MCP server builder</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMcpCore(
        this IServiceCollection services,
        McpServerBuilder builder
    )
    {
        // Create options from the builder
        var options = new McpServerOptions
        {
            Name = "MCP Server", // Default name
            Version = "1.0.0", // Default version
            Instructions = null,
            LogLevel = builder.LogLevel,
            UseConsoleLogging = builder.UseConsoleLogging,
            LogFilePath = builder.LogFilePath,
            NoAuthExplicitlyConfigured = builder.AuthHandler is NoAuthenticationHandler,
            Capabilities = null,
        };

        // Add assemblies from the builder
        foreach (var assembly in builder._assemblies)
        {
            try
            {
                var assemblyPath = assembly.Location;
                if (!string.IsNullOrEmpty(assemblyPath))
                {
                    options.ToolAssemblyPaths.Add(assemblyPath);
                }
            }
            catch (Exception)
            {
                // Ignore assemblies without a valid location
            }
        }

        // Register the server directly
        services.AddSingleton<McpServer>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("McpServerBuilder");

            logger.LogInformation("Building McpServer instance");
            var server = builder.Build();
            return server;
        });

        // Register the options for other services to use
        services.AddSingleton(options);
        services.Configure<McpServerOptions>(opt =>
        {
            opt.Name = options.Name;
            opt.Version = options.Version;
            opt.Instructions = options.Instructions;
            opt.LogLevel = options.LogLevel;
            opt.UseConsoleLogging = options.UseConsoleLogging;
            opt.LogFilePath = options.LogFilePath;
            opt.NoAuthExplicitlyConfigured = options.NoAuthExplicitlyConfigured;
            opt.ToolAssemblyPaths = new List<string>(options.ToolAssemblyPaths);
            opt.Capabilities = options.Capabilities;
        });

        return services;
    }

    /// <summary>
    /// Adds MCP logging services to the service collection with options configuration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Options configuration delegate</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMcpLogging(
        this IServiceCollection services,
        Action<LoggingOptions> configureOptions
    )
    {
        // Use the builder's logger factory if available, otherwise use the default
        if (services.Any(s => s.ServiceType == typeof(ILoggerFactory)))
        {
            // Logging is already configured, don't override it
            return services;
        }

        // Register options with the configuration delegate
        services.Configure(configureOptions);

        // Add the logging provider that uses the options
        services.AddSingleton<ILoggingProvider>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LoggingOptions>>().Value;
            return new LoggingProvider(options);
        });

        // Configure the logger factory
        services.AddSingleton<ILoggerFactory>(sp =>
            sp.GetRequiredService<ILoggingProvider>().CreateLoggerFactory()
        );

        // Register core logging configuration
        services.AddSingleton<IMcpLoggerConfiguration>(McpLoggerConfiguration.Instance);

        return services;
    }

    /// <summary>
    /// Adds MCP logging services to the service collection using options.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="options">The logging options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMcpLogging(
        this IServiceCollection services,
        LoggingOptions options
    )
    {
        return services.AddMcpLogging(opt =>
        {
            opt.MinimumLogLevel = options.MinimumLogLevel;
            opt.UseConsoleLogging = options.UseConsoleLogging;
            opt.LogFilePath = options.LogFilePath;
            opt.UseStdio = options.UseStdio;
            opt.PrettyConsoleOutput = options.PrettyConsoleOutput;
            opt.FileRollingInterval = options.FileRollingInterval;
            opt.FileSizeLimitBytes = options.FileSizeLimitBytes;
            opt.RetainedFileCountLimit = options.RetainedFileCountLimit;
            opt.ComponentLogLevels = new Dictionary<string, LogLevel>(options.ComponentLogLevels);
        });
    }

    /// <summary>
    /// Adds MCP logging services to the service collection.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="builder">The MCP server builder</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMcpLogging(
        this IServiceCollection services,
        McpServerBuilder builder
    )
    {
        // Use the builder's logger factory if available, otherwise use the default
        if (services.Any(s => s.ServiceType == typeof(ILoggerFactory)))
        {
            // Logging is already configured, don't override it
            return services;
        }

        // Create logging options from the builder settings
        var loggingOptions = new LoggingOptions
        {
            MinimumLogLevel = builder.LogLevel,
            UseConsoleLogging = builder.UseConsoleLogging,
            LogFilePath = builder.LogFilePath,
            // Other options use defaults
        };

        return services.AddMcpLogging(loggingOptions);
    }

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
                    loggerFactory
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
            var authOptions = GetAuthOptionsFromBuilder(builder);
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

    /// <summary>
    /// Adds MCP tool registration services to the service collection with options configuration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Options configuration delegate</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMcpTools(
        this IServiceCollection services,
        Action<ToolRegistrationOptions> configureOptions
    )
    {
        // Register options with the configuration delegate
        services.Configure(configureOptions);

        // Add the tool registry
        services.AddSingleton<ToolRegistry>();

        // Configure tool discovery and registration using IOptions
        services.AddSingleton<IStartupFilter>(sp =>
        {
            return new ActionStartupFilter(() =>
            {
                var server = sp.GetRequiredService<McpServer>();
                var toolRegistry = sp.GetRequiredService<ToolRegistry>();
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger("ToolRegistration");
                var toolOptions = sp.GetRequiredService<IOptions<ToolRegistrationOptions>>().Value;

                // Add entry assembly if configured
                if (toolOptions.IncludeEntryAssembly)
                {
                    var entryAssembly = Assembly.GetEntryAssembly();
                    if (entryAssembly != null)
                    {
                        logger.LogInformation(
                            "Adding entry assembly to tool registry: {AssemblyName}",
                            entryAssembly.FullName
                        );
                        toolRegistry.AddAssembly(entryAssembly);
                    }
                }

                // Add all configured assemblies to the registry
                if (toolOptions.Assemblies.Count > 0)
                {
                    logger.LogInformation(
                        "Adding {Count} configured assemblies to tool registry",
                        toolOptions.Assemblies.Count
                    );

                    foreach (var assembly in toolOptions.Assemblies)
                    {
                        logger.LogInformation(
                            "Adding configured assembly to tool registry: {AssemblyName}",
                            assembly.GetName().Name
                        );
                        toolRegistry.AddAssembly(assembly);
                    }
                }

                // Configure detailed logging if enabled
                if (toolOptions.EnableDetailedLogging)
                {
                    logger.LogInformation("Detailed tool registration logging is enabled");
                }

                // Register all tools with the server
                logger.LogInformation("Registering tools with server");
                toolRegistry.RegisterToolsWithServer(server);
            });
        });

        return services;
    }

    /// <summary>
    /// Adds MCP tool registration services to the service collection using options.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="options">The tool registration options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMcpTools(
        this IServiceCollection services,
        ToolRegistrationOptions options
    )
    {
        services.Configure<ToolRegistrationOptions>(opt =>
        {
            opt.IncludeEntryAssembly = options.IncludeEntryAssembly;
            opt.EnableDetailedLogging = options.EnableDetailedLogging;
            opt.ValidateToolMethods = options.ValidateToolMethods;

            // Add all assemblies
            foreach (var assembly in options.Assemblies)
            {
                opt.Assemblies.Add(assembly);
            }
        });

        return services.AddMcpTools(opt => { });
    }

    /// <summary>
    /// Adds MCP tool registration services to the service collection.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="builder">The MCP server builder</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMcpTools(
        this IServiceCollection services,
        McpServerBuilder builder
    )
    {
        return services.AddMcpTools(options =>
        {
            options.IncludeEntryAssembly = true;
            options.Assemblies.AddRange(builder._assemblies);
        });
    }

    /// <summary>
    /// Adds CORS services if they haven't been registered already.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMcpCorsServices(this IServiceCollection services)
    {
        // Register CORS services if they haven't been registered already
        // This ensures CORS middleware will work when enabled
        if (
            !services.Any(s =>
                s.ServiceType == typeof(Microsoft.AspNetCore.Cors.Infrastructure.ICorsService)
            )
        )
        {
            services.AddCors();
        }

        return services;
    }

    /// <summary>
    /// Adds SSE transport services to the service collection with options configuration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Options configuration delegate</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMcpSseTransport(
        this IServiceCollection services,
        Action<SseServerOptions> configureOptions
    )
    {
        // Register options
        services.Configure(configureOptions);

        // Add connection manager
        services.AddSingleton<SseConnectionManager>(sp =>
        {
            var server = sp.GetRequiredService<McpServer>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var options = sp.GetRequiredService<IOptions<SseServerOptions>>().Value;

            // Create a connection manager with timeout from options or default
            var connectionTimeout = options.ConnectionTimeout.GetValueOrDefault(
                TimeSpan.FromMinutes(30)
            );

            // Get auth handler from service provider
            var authHandler = sp.GetService<IAuthHandler>();

            return new SseConnectionManager(server, loggerFactory, connectionTimeout, authHandler);
        });

        // Add transport factory
        services.AddSingleton<ISseTransportFactory, SseTransportFactory>();

        // Register server configuration based on SSE options
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<SseServerOptions>>().Value;
            return McpServerConfiguration.FromSseServerOptions(options);
        });

        // Add hosted service
        services.AddHostedService<McpServerHostedService>();

        return services;
    }

    /// <summary>
    /// Adds SSE transport services to the service collection using options.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="options">The SSE server options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMcpSseTransport(
        this IServiceCollection services,
        SseServerOptions options
    )
    {
        return services.AddMcpSseTransport(opt =>
        {
            opt.Name = options.Name;
            opt.Version = options.Version;
            opt.Instructions = options.Instructions;
            opt.Hostname = options.Hostname;
            opt.Port = options.Port;
            opt.ConnectionTimeout = options.ConnectionTimeout;
            opt.ApiKeyOptions = options.ApiKeyOptions;
            opt.LogLevel = options.LogLevel;
            opt.LogFilePath = options.LogFilePath;
            opt.UseConsoleLogging = options.UseConsoleLogging;
            opt.Capabilities = options.Capabilities;
        });
    }

    /// <summary>
    /// Adds SSE transport services to the service collection.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="sseBuilder">The SSE transport builder</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMcpSseTransport(
        this IServiceCollection services,
        SseServerBuilder sseBuilder
    )
    {
        // If builder has options, use them directly
        if (sseBuilder.Options != null)
        {
            return services.AddMcpSseTransport(sseBuilder.Options);
        }

        // Otherwise, create options from the builder's properties
        var options = new SseServerOptions
        {
            Hostname = sseBuilder.Hostname,
            Port = sseBuilder.HostPort,
        };

        return services.AddMcpSseTransport(options);
    }

    /// <summary>
    /// Adds stdio transport services to the service collection with options configuration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Options configuration delegate</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMcpStdioTransport(
        this IServiceCollection services,
        Action<StdioServerOptions> configureOptions
    )
    {
        // Register options
        services.Configure(configureOptions);

        // Register hosted service
        services.AddSingleton<McpServerHostedService>();

        // Register server configuration
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<StdioServerOptions>>().Value;
            return new McpServerConfiguration
            {
                Hostname = "localhost", // Default for stdio
            };
        });

        return services;
    }

    /// <summary>
    /// Adds stdio transport services to the service collection using options.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="options">The stdio server options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMcpStdioTransport(
        this IServiceCollection services,
        StdioServerOptions options
    )
    {
        return services.AddMcpStdioTransport(opt =>
        {
            opt.Name = options.Name;
            opt.Version = options.Version;
            opt.Instructions = options.Instructions;
            opt.LogLevel = options.LogLevel;
            opt.LogFilePath = options.LogFilePath;
            opt.UseConsoleLogging = options.UseConsoleLogging;
            opt.Capabilities = options.Capabilities;
        });
    }

    /// <summary>
    /// Adds stdio transport services to the service collection.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="builder">The MCP server builder</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMcpStdioTransport(
        this IServiceCollection services,
        McpServerBuilder builder
    )
    {
        // Create options from the builder
        var options = new StdioServerOptions
        {
            Name = "MCP Server", // Default name
            Version = "1.0.0", // Default version
            Instructions = null,
            LogLevel = builder.LogLevel,
            LogFilePath = builder.LogFilePath,
            UseConsoleLogging = builder.UseConsoleLogging,
            Capabilities = null,
        };

        return services.AddMcpStdioTransport(options);
    }

    /// <summary>
    /// Extract AuthOptions from the builder if available
    /// </summary>
    private static AuthOptions? GetAuthOptionsFromBuilder(McpServerBuilder builder)
    {
        // If the builder has an API Key handler, it should have options
        if (builder.AuthHandler is ApiKeyAuthenticationHandler apiKeyHandler)
        {
            // Create AuthOptions from ApiKeyAuthOptions
            var apiKeyOptions = apiKeyHandler.Options;
            return new AuthOptions
            {
                Enabled = apiKeyOptions.Enabled,
                SchemeName = apiKeyOptions.SchemeName,
                SecuredPaths = apiKeyOptions.SecuredPaths,
                EnableLogging = apiKeyOptions.EnableLogging,
            };
        }

        // If using SSE transport, check if it has API key options
        if (
            builder.TransportBuilder is SseServerBuilder sseBuilder
            && sseBuilder.Options?.ApiKeyOptions != null
        )
        {
            var apiKeyOptions = sseBuilder.Options.ApiKeyOptions;
            return new AuthOptions
            {
                Enabled = apiKeyOptions.Enabled,
                SchemeName = apiKeyOptions.SchemeName,
                SecuredPaths = apiKeyOptions.SecuredPaths,
                EnableLogging = apiKeyOptions.EnableLogging,
            };
        }

        // If no specific options are available, return one with common secured paths
        return new AuthOptions
        {
            Enabled = true,
            SecuredPaths = new List<string> { "/sse", "/messages" },
            EnableLogging = true,
        };
    }
}

/// <summary>
/// Helper class to run actions during application startup.
/// </summary>
internal class ActionStartupFilter : IStartupFilter
{
    private readonly Action _action;

    public ActionStartupFilter(Action action)
    {
        _action = action;
    }

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return builder =>
        {
            _action();
            next(builder);
        };
    }
}
