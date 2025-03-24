using System;
using System.Threading.Tasks;
using Mcp.Net.Core.JsonRpc;

namespace Mcp.Net.Core.Interfaces;

/// <summary>
/// High-level interface for JSON-RPC transport
/// </summary>
public interface ITransport
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
    /// Event triggered when an error occurs
    /// </summary>
    event Action<Exception>? OnError;

    /// <summary>
    /// Event triggered when the transport is closed
    /// </summary>
    event Action? OnClose;

    /// <summary>
    /// Starts the transport
    /// </summary>
    /// <returns>A task representing the asynchronous start operation</returns>
    Task StartAsync();

    /// <summary>
    /// Sends a JSON-RPC response
    /// </summary>
    /// <param name="message">The response to send</param>
    /// <returns>A task representing the asynchronous send operation</returns>
    Task SendAsync(JsonRpcResponseMessage message);

    /// <summary>
    /// Closes the transport
    /// </summary>
    /// <returns>A task representing the asynchronous close operation</returns>
    Task CloseAsync();
}
