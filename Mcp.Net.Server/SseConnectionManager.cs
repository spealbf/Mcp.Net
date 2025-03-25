using System.Collections.Concurrent;

namespace Mcp.Net.Server;

/// <summary>
/// Manages SSE connections for server-client communication
/// </summary>
internal class SseConnectionManager
{
    private readonly ConcurrentDictionary<string, SseTransport> _connections = new();
    private readonly ILogger<SseConnectionManager> _logger;
    private readonly Timer _cleanupTimer;
    private readonly TimeSpan _connectionTimeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="SseConnectionManager"/> class
    /// </summary>
    /// <param name="logger">Logger</param>
    /// <param name="connectionTimeout">Optional timeout for stale connections</param>
    public SseConnectionManager(
        ILogger<SseConnectionManager> logger,
        TimeSpan? connectionTimeout = null
    )
    {
        _logger = logger;
        _connectionTimeout = connectionTimeout ?? TimeSpan.FromMinutes(30);

        // Create a timer to periodically check for stale connections
        _cleanupTimer = new Timer(
            CleanupStaleConnections,
            null,
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(5)
        );
    }

    /// <summary>
    /// Gets a transport by session ID
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <returns>The transport, or null if not found</returns>
    public SseTransport? GetTransport(string sessionId)
    {
        if (_connections.TryGetValue(sessionId, out var transport))
        {
            return transport;
        }

        _logger.LogWarning("Transport not found for session ID: {SessionId}", sessionId);
        return null;
    }

    /// <summary>
    /// Gets all connected transports
    /// </summary>
    /// <returns>Collection of all active transports</returns>
    public IReadOnlyCollection<SseTransport> GetAllTransports()
    {
        return _connections.Values.ToArray();
    }

    /// <summary>
    /// Registers a transport with the connection manager
    /// </summary>
    /// <param name="transport">The transport to register</param>
    public void RegisterTransport(SseTransport transport)
    {
        _connections[transport.SessionId] = transport;
        _logger.LogInformation(
            "Registered transport with session ID: {SessionId}",
            transport.SessionId
        );

        // Remove the transport when it closes
        transport.OnClose += () =>
        {
            _logger.LogInformation(
                "Transport closed, removing from connection manager: {SessionId}",
                transport.SessionId
            );
            RemoveTransport(transport.SessionId);
        };
    }

    /// <summary>
    /// Removes a transport from the connection manager
    /// </summary>
    /// <param name="sessionId">The session ID to remove</param>
    /// <returns>True if the transport was found and removed, false otherwise</returns>
    public bool RemoveTransport(string sessionId)
    {
        return _connections.TryRemove(sessionId, out _);
    }

    /// <summary>
    /// Closes all connections and disposes resources
    /// </summary>
    public async Task CloseAllConnectionsAsync()
    {
        _logger.LogInformation("Closing all SSE connections...");
        _cleanupTimer.Dispose();

        // Create a copy of the connections to avoid enumeration issues
        var transportsCopy = _connections.Values.ToArray();

        // Close each transport
        var closeTasks = transportsCopy
            .Select(async transport =>
            {
                try
                {
                    await transport.CloseAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error closing transport: {SessionId}",
                        transport.SessionId
                    );
                }
            })
            .ToArray();

        // Wait for all connections to close with a timeout
        await Task.WhenAll(closeTasks).WaitAsync(TimeSpan.FromSeconds(10));

        // Clear the connections dictionary
        _connections.Clear();
    }

    /// <summary>
    /// Periodically checks for and removes stale connections
    /// </summary>
    private void CleanupStaleConnections(object? state)
    {
        try
        {
            _logger.LogDebug("Checking for stale connections...");

            // In a real implementation, we would track last activity time for each connection
            // and remove those that have been inactive for longer than the timeout.
            // For now, since we don't have a way to track activity, we'll just log the count.

            _logger.LogDebug("Active connections: {ConnectionCount}", _connections.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up stale connections");
        }
    }
}
