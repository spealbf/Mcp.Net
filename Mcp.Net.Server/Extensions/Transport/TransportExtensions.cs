using Mcp.Net.Server.ServerBuilder;

namespace Mcp.Net.Server.Extensions.Transport;

/// <summary>
/// Extension methods for configuring MCP transport services.
/// </summary>
public static class TransportExtensions
{
    /// <summary>
    /// Registers a hosted service to start and manage the MCP server.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMcpServerHostedService(this IServiceCollection services)
    {
        // Register the hosted service if not already registered
        if (!services.Any(s => s.ServiceType == typeof(McpServerHostedService)))
        {
            services.AddHostedService<McpServerHostedService>();
        }

        return services;
    }
}