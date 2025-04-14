using Mcp.Net.Core.Interfaces;
using Mcp.Net.Core.Models.Capabilities;
using Mcp.Net.Core.Transport;
using Mcp.Net.Server.Authentication;
using Mcp.Net.Server.Logging;
using Mcp.Net.Server.Transport.Sse;
using Mcp.Net.Server.Transport.Stdio;


namespace Mcp.Net.Server.ServerBuilder;

/// <summary>
/// Extension methods for adding MCP server capabilities to an application
/// </summary>
public static class McpServerServiceCollectionExtensions
{
    /// <summary>
    /// Adds an MCP server to the application builder
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <returns>The application builder for chaining</returns>
    public static IApplicationBuilder UseMcpServer(this IApplicationBuilder app)
    {
        app.UseMiddleware<McpAuthenticationMiddleware>();

        app.Map("/sse", sseApp => sseApp.UseMiddleware<SseConnectionMiddleware>());

        app.Map("/messages", messagesApp => messagesApp.UseMiddleware<SseMessageMiddleware>());

        return app;
    }

    /// <summary>
    /// Adds MCP server services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Builder configuration delegate</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMcpServer(
        this IServiceCollection services,
        Action<McpServerBuilder> configure
    )
    {
        var builder = new McpServerBuilder();
        configure(builder);

        // Register logging services
        services.AddSingleton<IMcpLoggerConfiguration>(McpLoggerConfiguration.Instance);
        services.AddSingleton<ILoggerFactory>(sp =>
            McpLoggerConfiguration.Instance.CreateLoggerFactory()
        );

        // Register authentication services if configured
        if (builder.Authentication != null)
        {
            services.AddSingleton<IAuthentication>(builder.Authentication);
        }

        if (builder.ApiKeyValidator != null)
        {
            services.AddSingleton<IApiKeyValidator>(builder.ApiKeyValidator);
            
            // Register API key authentication if a validator is configured but no explicit authentication is provided
            if (builder.Authentication == null)
            {
                services.AddSingleton<IAuthentication>(sp => 
                {
                    var options = new ApiKeyAuthOptions 
                    { 
                        HeaderName = "X-API-Key",
                        QueryParamName = "api_key"
                    };
                    var validator = sp.GetRequiredService<IApiKeyValidator>();
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                    return new ApiKeyAuthenticationHandler(
                        options, 
                        validator,
                        loggerFactory.CreateLogger<ApiKeyAuthenticationHandler>()
                    );
                });
            }
        }

        // Register server as singleton and configure tools
        services.AddSingleton<McpServer>(sp => 
        {
            var server = builder.Build();
            
            // Register tools from additional assemblies
            foreach (var assembly in builder._additionalToolAssemblies)
            {
                server.RegisterToolsFromAssembly(assembly, sp);
            }
            
            return server;
        });

        if (builder.IsUsingSse)
        {
            // Register SSE-specific services
            _ = services.AddSingleton<SseConnectionManager>(sp =>
            {
                var server = sp.GetRequiredService<McpServer>();
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                var authentication = sp.GetService<IAuthentication>(); // Get the authentication handler if registered
                return new SseConnectionManager(
                    server,
                    loggerFactory,
                    TimeSpan.FromMinutes(30),
                    authentication
                );
            });
            services.AddSingleton<ISseTransportFactory, SseTransportFactory>();

            services.AddSingleton(
                new McpServerConfiguration { Port = builder.Port, Hostname = builder.Hostname }
            );
        }
        else
        {
            // Register standard stdio transport for hosted service
            services.AddSingleton<IServerTransport>(sp => new StdioTransport());
        }

        services.AddHostedService<McpServerHostedService>();

        return services;
    }
}
