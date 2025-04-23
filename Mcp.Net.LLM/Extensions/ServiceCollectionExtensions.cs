using Mcp.Net.LLM.Agents;
using Mcp.Net.LLM.Agents.Stores;
using Mcp.Net.LLM.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.LLM.Extensions;

/// <summary>
/// Extension methods for registering Agent services with dependency injection
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds core agent services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddAgentServices(this IServiceCollection services)
    {
        // Register the registry as a singleton
        services.AddSingleton<IAgentRegistry, AgentRegistry>();

        return services;
    }

    /// <summary>
    /// Adds in-memory agent store implementation
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddInMemoryAgentStore(this IServiceCollection services)
    {
        services.AddSingleton<IAgentStore, InMemoryAgentStore>();
        return services;
    }

    /// <summary>
    /// Adds file system agent store implementation
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="storageDirectory">Directory where agent definitions will be stored</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddFileSystemAgentStore(
        this IServiceCollection services,
        string storageDirectory
    )
    {
        services.AddSingleton<IAgentStore>(sp => new FileSystemAgentStore(
            storageDirectory,
            sp.GetRequiredService<ILogger<FileSystemAgentStore>>()
        ));

        return services;
    }
}
