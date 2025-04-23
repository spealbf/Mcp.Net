using Mcp.Net.LLM.Models;

namespace Mcp.Net.LLM.Interfaces;

/// <summary>
/// Interface for agent registry that manages agent definitions
/// </summary>
public interface IAgentRegistry
{
    /// <summary>
    /// Event raised when an agent is registered
    /// </summary>
    event EventHandler<AgentDefinition> AgentRegistered;

    /// <summary>
    /// Event raised when an agent is unregistered
    /// </summary>
    event EventHandler<string> AgentUnregistered;

    /// <summary>
    /// Event raised when all agents are reloaded
    /// </summary>
    event EventHandler AgentsReloaded;

    /// <summary>
    /// Gets all registered agents
    /// </summary>
    /// <returns>Collection of agent definitions</returns>
    Task<IEnumerable<AgentDefinition>> GetAllAgentsAsync();

    /// <summary>
    /// Gets an agent by its ID
    /// </summary>
    /// <param name="id">The unique ID of the agent</param>
    /// <returns>The agent definition if found, null otherwise</returns>
    Task<AgentDefinition?> GetAgentByIdAsync(string id);

    /// <summary>
    /// Gets all agents in a specific category
    /// </summary>
    /// <param name="category">The category to filter by</param>
    /// <returns>Collection of agent definitions in the specified category</returns>
    Task<IEnumerable<AgentDefinition>> GetAgentsByCategoryAsync(AgentCategory category);

    /// <summary>
    /// Registers a new agent in the registry
    /// </summary>
    /// <param name="agent">The agent definition to register</param>
    /// <returns>True if successful, false otherwise</returns>
    Task<bool> RegisterAgentAsync(AgentDefinition agent);

    /// <summary>
    /// Unregisters an agent from the registry
    /// </summary>
    /// <param name="id">The unique ID of the agent to unregister</param>
    /// <returns>True if successful, false if agent not found</returns>
    Task<bool> UnregisterAgentAsync(string id);

    /// <summary>
    /// Reloads all agents from the underlying store
    /// </summary>
    Task ReloadAgentsAsync();
}
