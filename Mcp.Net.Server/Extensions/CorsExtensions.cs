using Microsoft.Extensions.DependencyInjection;

namespace Mcp.Net.Server.Extensions;

/// <summary>
/// Extension methods for configuring CORS services for MCP servers.
/// </summary>
public static class CorsExtensions
{
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
}