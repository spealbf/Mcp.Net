using System.Reflection;
using Mcp.Net.Core.Interfaces;
using Mcp.Net.Core.Models.Capabilities;
using Mcp.Net.Core.Transport;
using Mcp.Net.Server.Authentication;
using Mcp.Net.Server.Extensions;
using Mcp.Net.Server.Logging;
using Mcp.Net.Server.Transport.Sse;
using Mcp.Net.Server.Transport.Stdio;

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

        RegisterLoggingServices(services, builder);

        RegisterAuthenticationServices(services, builder);

        RegisterServerAndTools(services, builder);

        RegisterSseServices(services, sseBuilder);

        return services;
    }

    /// <summary>
    /// Registers logging services with the service collection.
    /// </summary>
    private static void RegisterLoggingServices(
        IServiceCollection services,
        McpServerBuilder builder
    )
    {
        // Use the builder's logger factory if available, otherwise use the default
        if (services.Any(s => s.ServiceType == typeof(ILoggerFactory)))
        {
            // Logging is already configured, don't override it
            return;
        }

        // Create logging options from the builder settings
        var loggingOptions = new Options.LoggingOptions
        {
            MinimumLogLevel = builder.LogLevel,
            UseConsoleLogging = builder.UseConsoleLogging,
            LogFilePath = builder.LogFilePath,
            // Other options use defaults
        };

        // Register options
        services.Configure<Options.LoggingOptions>(options =>
        {
            options.MinimumLogLevel = loggingOptions.MinimumLogLevel;
            options.UseConsoleLogging = loggingOptions.UseConsoleLogging;
            options.LogFilePath = loggingOptions.LogFilePath;
            options.UseStdio = loggingOptions.UseStdio;
        });

        // Register logging provider
        services.AddSingleton<Options.ILoggingProvider>(sp => new Options.LoggingProvider(
            loggingOptions
        ));

        // Register logger factory
        services.AddSingleton<ILoggerFactory>(sp =>
            sp.GetRequiredService<Options.ILoggingProvider>().CreateLoggerFactory()
        );

        // For backward compatibility
        services.AddSingleton<IMcpLoggerConfiguration>(McpLoggerConfiguration.Instance);
    }

    /// <summary>
    /// Registers authentication services with the service collection.
    /// </summary>
    private static void RegisterAuthenticationServices(
        IServiceCollection services,
        McpServerBuilder builder
    )
    {
        // Register API key validator if configured
        if (builder.ApiKeyValidator != null)
        {
            services.AddSingleton<IApiKeyValidator>(builder.ApiKeyValidator);
        }

        // Register authentication handler if configured
        if (builder.Authentication != null)
        {
            services.AddSingleton<IAuthentication>(builder.Authentication);
            return;
        }

        // Configure authentication using the API key validator if available
        if (builder.ApiKeyValidator != null)
        {
            services.AddSingleton<IAuthentication>(sp =>
            {
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                var options = new ApiKeyAuthOptions
                {
                    HeaderName = "X-API-Key",
                    QueryParamName = "api_key",
                };

                return new ApiKeyAuthenticationHandler(
                    options,
                    builder.ApiKeyValidator,
                    loggerFactory.CreateLogger<ApiKeyAuthenticationHandler>()
                );
            });
        }
    }

    /// <summary>
    /// Registers the server and tools with the service collection.
    /// </summary>
    private static void RegisterServerAndTools(
        IServiceCollection services,
        McpServerBuilder builder
    )
    {
        services.AddSingleton<McpServer>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("McpServerBuilder");

            var server = builder.Build();

            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly != null)
            {
                logger.LogInformation(
                    "Scanning entry assembly for tools: {AssemblyName}",
                    entryAssembly.FullName
                );
                server.RegisterToolsFromAssembly(entryAssembly, sp);
            }

            foreach (var assembly in builder._additionalToolAssemblies)
            {
                server.RegisterToolsFromAssembly(assembly, sp);
            }

            return server;
        });
    }

    /// <summary>
    /// Registers SSE-specific services with the service collection.
    /// </summary>
    private static void RegisterSseServices(
        IServiceCollection services,
        SseServerBuilder sseBuilder
    )
    {
        services.AddSingleton<SseConnectionManager>(sp =>
        {
            var server = sp.GetRequiredService<McpServer>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var authentication = sp.GetService<IAuthentication>();

            // Create a connection manager with timeout from options or default
            var connectionTimeout =
                sseBuilder.Options?.ConnectionTimeout ?? TimeSpan.FromMinutes(30);

            return new SseConnectionManager(
                server,
                loggerFactory,
                connectionTimeout,
                authentication
            );
        });

        services.AddSingleton<ISseTransportFactory, SseTransportFactory>();

        // Add our server options to the service collection
        if (sseBuilder.Options != null)
        {
            services.AddSingleton(sseBuilder.Options);

            // For backward compatibility, also register McpServerConfiguration
            services.AddSingleton(McpServerConfiguration.FromSseServerOptions(sseBuilder.Options));
        }
        else
        {
            // Create options from the builder's properties
            var options = new Options.SseServerOptions
            {
                Hostname = sseBuilder.Hostname,
                Port = sseBuilder.HostPort,
            };
            services.AddSingleton(options);

            // For backward compatibility, also register McpServerConfiguration
            services.AddSingleton(
                new McpServerConfiguration
                {
                    Port = sseBuilder.HostPort,
                    Hostname = sseBuilder.Hostname,
                }
            );
        }

        services.AddHostedService<McpServerHostedService>();
    }
}
