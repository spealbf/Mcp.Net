using Mcp.Net.Server.Options;
using Mcp.Net.Server.ServerBuilder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Mcp.Net.Server.Extensions.Transport;

/// <summary>
/// Extension methods for configuring STDIO transport services.
/// </summary>
public static class StdioTransportExtensions
{
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
}