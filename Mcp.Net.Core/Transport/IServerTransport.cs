using System;
using System.Threading.Tasks;
using Mcp.Net.Core.Interfaces;
using Mcp.Net.Core.JsonRpc;

namespace Mcp.Net.Core.Transport;

/// <summary>
/// Interface for server-specific transport operations
/// </summary>
public interface IServerTransport : ITransport
{
    /// <summary>
    /// Event triggered when a request is received
    /// </summary>
    event Action<JsonRpcRequestMessage>? OnRequest;

    /// <summary>
    /// Event triggered when a notification is received
    /// </summary>
    event Action<JsonRpcNotificationMessage>? OnNotification;

    /// <summary>
    /// Sends a JSON-RPC response to the client
    /// </summary>
    /// <param name="message">The response to send</param>
    /// <returns>A task representing the asynchronous send operation</returns>
    Task SendAsync(JsonRpcResponseMessage message);
}
