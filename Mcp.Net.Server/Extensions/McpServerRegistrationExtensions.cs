using Mcp.Net.Server.ServerBuilder;

namespace Mcp.Net.Server.Extensions;

/// <summary>
/// Extension methods for registering the MCP server with the dependency injection container.
/// </summary>
public static class McpServerRegistrationExtensions
{
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
        // Use fully qualified name to avoid ambiguity
        Mcp.Net.Server.Extensions.Transport.SseTransportExtensions.AddMcpSseTransport(services, sseBuilder);

        return services;
    }
}