using System.Reflection;
using Mcp.Net.Server.Options;
using Mcp.Net.Server.ServerBuilder.Helpers;
using Microsoft.Extensions.Options;

namespace Mcp.Net.Server.Extensions;

/// <summary>
/// Extension methods for configuring tool registration for MCP servers.
/// </summary>
public static class ToolRegistrationExtensions
{
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
        ServerBuilder.McpServerBuilder builder
    )
    {
        return services.AddMcpTools(options =>
        {
            options.IncludeEntryAssembly = true;
            options.Assemblies.AddRange(builder._assemblies);
        });
    }
}
