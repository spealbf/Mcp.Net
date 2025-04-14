using Mcp.Net.Client.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.Client;

/// <summary>
/// Extension methods for adding MCP client to DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds an MCP client to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add the client to.</param>
    /// <param name="configureClient">Action to configure the client.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddMcpClient(
        this IServiceCollection services,
        Action<McpClientBuilder> configureClient)
    {
        services.AddSingleton<IMcpClient>(sp =>
        {
            var builder = new McpClientBuilder();

            // Add logger if available
            var logger = sp.GetService<ILoggerFactory>()?.CreateLogger("Mcp.Client");
            if (logger != null)
            {
                builder.WithLogger(logger);
            }

            // Configure the client
            configureClient(builder);

            // Build the client (initialization will happen when the service starts)
            return builder.Build();
        });

        return services;
    }

    /// <summary>
    /// Adds an MCP client to the service collection and initializes it.
    /// </summary>
    /// <param name="services">The service collection to add the client to.</param>
    /// <param name="configureClient">Action to configure the client.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddMcpClientWithInitialization(
        this IServiceCollection services,
        Action<McpClientBuilder> configureClient)
    {
        services.AddSingleton<IMcpClient>(sp =>
        {
            var builder = new McpClientBuilder();

            // Add logger if available
            var logger = sp.GetService<ILoggerFactory>()?.CreateLogger("Mcp.Client");
            if (logger != null)
            {
                builder.WithLogger(logger);
            }

            // Configure the client
            configureClient(builder);

            // Build and initialize the client - this blocks until initialization is complete
            return builder.BuildAndInitializeAsync().GetAwaiter().GetResult();
        });

        return services;
    }

    /// <summary>
    /// Adds an MCP client to the service collection with deferred initialization.
    /// </summary>
    /// <param name="services">The service collection to add the client to.</param>
    /// <param name="configureClient">Action to configure the client.</param>
    /// <returns>The service collection.</returns>
    /// <remarks>
    /// This method registers a Lazy&lt;Task&lt;IMcpClient&gt;&gt; that initializes the client
    /// the first time it's requested. This is useful for scenarios where you want
    /// to defer initialization until the client is actually needed.
    /// </remarks>
    public static IServiceCollection AddLazyMcpClient(
        this IServiceCollection services,
        Action<McpClientBuilder> configureClient)
    {
        services.AddSingleton<Lazy<Task<IMcpClient>>>(sp =>
        {
            return new Lazy<Task<IMcpClient>>(async () =>
            {
                var builder = new McpClientBuilder();

                // Add logger if available
                var logger = sp.GetService<ILoggerFactory>()?.CreateLogger("Mcp.Client");
                if (logger != null)
                {
                    builder.WithLogger(logger);
                }

                // Configure the client
                configureClient(builder);

                // Build and initialize the client
                return await builder.BuildAndInitializeAsync();
            });
        });

        return services;
    }
}