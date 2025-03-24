using System.Collections.Concurrent;
using Mcp.Net.Server;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.Server;

/// <summary>
/// Manages SSE connections for server-client communication
/// </summary>
internal class SseConnectionManager
{
    private readonly ConcurrentDictionary<string, SseTransport> _connections = new();
    private readonly ILogger<SseConnectionManager> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SseConnectionManager"/> class
    /// </summary>
    /// <param name="logger">Logger</param>
    public SseConnectionManager(ILogger<SseConnectionManager> logger)
    {
        _logger = logger;
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
            _connections.TryRemove(transport.SessionId, out _);
        };
    }
}
