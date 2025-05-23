using System;
using System.Threading.Tasks;
using Mcp.Net.Core.Interfaces;
using Mcp.Net.Core.JsonRpc;

namespace Mcp.Net.Core.Transport;

/// <summary>
/// Interface for client-specific transport operations
/// </summary>
public interface IClientTransport : ITransport
{
    /// <summary>
    /// Event triggered when a response is received
    /// </summary>
    event Action<JsonRpcResponseMessage>? OnResponse;

    /// <summary>
    /// Sends a JSON-RPC request to the server and returns the response
    /// </summary>
    /// <param name="method">The method name to invoke</param>
    /// <param name="parameters">Optional parameters for the method</param>
    /// <returns>A task that completes with the response object</returns>
    Task<object> SendRequestAsync(string method, object? parameters = null);

    /// <summary>
    /// Sends a JSON-RPC notification to the server
    /// </summary>
    /// <param name="method">The method name for the notification</param>
    /// <param name="parameters">Optional parameters for the notification</param>
    /// <returns>A task that completes when the notification is sent</returns>
    Task SendNotificationAsync(string method, object? parameters = null);
}
