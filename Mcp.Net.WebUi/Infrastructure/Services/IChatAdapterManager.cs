using Mcp.Net.WebUi.Adapters.Interfaces;

namespace Mcp.Net.WebUi.Infrastructure.Services;

/// <summary>
/// Interface for managing chat adapters across sessions
/// </summary>
public interface IChatAdapterManager
{
    /// <summary>
    /// Gets an existing adapter or creates a new one for the specified session
    /// </summary>
    /// <param name="sessionId">Session identifier</param>
    /// <param name="createFunc">Function to create a new adapter if one doesn't exist</param>
    Task<ISignalRChatAdapter> GetOrCreateAdapterAsync(
        string sessionId,
        Func<string, Task<ISignalRChatAdapter>> createFunc
    );

    /// <summary>
    /// Marks an adapter as active, updating its last activity timestamp
    /// </summary>
    void MarkAdapterAsActive(string sessionId);

    /// <summary>
    /// Removes an adapter for the specified session
    /// </summary>
    Task RemoveAdapterAsync(string sessionId);

    /// <summary>
    /// Gets all active session IDs
    /// </summary>
    IEnumerable<string> GetActiveSessions();
}
