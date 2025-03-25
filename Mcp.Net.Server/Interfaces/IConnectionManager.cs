using Mcp.Net.Core.Interfaces;

namespace Mcp.Net.Server.Interfaces;

/// <summary>
/// Interface for managing transport connections
/// </summary>
public interface IConnectionManager
{
    /// <summary>
    /// Gets a transport by session ID
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <returns>The transport, or null if not found</returns>
    Task<ITransport?> GetTransportAsync(string sessionId);

    /// <summary>
    /// Registers a transport with the connection manager
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="transport">The transport to register</param>
    Task RegisterTransportAsync(string sessionId, ITransport transport);

    /// <summary>
    /// Removes a transport from the connection manager
    /// </summary>
    /// <param name="sessionId">The session ID to remove</param>
    /// <returns>True if the transport was found and removed, false otherwise</returns>
    Task<bool> RemoveTransportAsync(string sessionId);

    /// <summary>
    /// Closes all connections and disposes resources
    /// </summary>
    Task CloseAllConnectionsAsync();
}