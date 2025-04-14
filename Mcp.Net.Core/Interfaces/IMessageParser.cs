using System;
using Mcp.Net.Core.JsonRpc;

namespace Mcp.Net.Core.Interfaces;

/// <summary>
/// Interface for parsing JSON-RPC messages
/// </summary>
public interface IMessageParser
{
    /// <summary>
    /// Attempts to parse a complete message from the input
    /// </summary>
    /// <param name="input">Input span to parse</param>
    /// <param name="message">The parsed message, if successful</param>
    /// <param name="consumed">Number of characters consumed from the input</param>
    /// <returns>True if a complete message was parsed, false otherwise</returns>
    bool TryParseMessage(ReadOnlySpan<char> input, out string message, out int consumed);

    /// <summary>
    /// Determines if the message is a JSON-RPC request
    /// </summary>
    /// <param name="message">JSON message to check</param>
    /// <returns>True if the message is a JSON-RPC request</returns>
    bool IsJsonRpcRequest(string message);

    /// <summary>
    /// Determines if the message is a JSON-RPC notification
    /// </summary>
    /// <param name="message">JSON message to check</param>
    /// <returns>True if the message is a JSON-RPC notification</returns>
    bool IsJsonRpcNotification(string message);

    /// <summary>
    /// Determines if the message is a JSON-RPC response
    /// </summary>
    /// <param name="message">JSON message to check</param>
    /// <returns>True if the message is a JSON-RPC response</returns>
    bool IsJsonRpcResponse(string message);

    /// <summary>
    /// Deserializes JSON to a specified type
    /// </summary>
    /// <typeparam name="TMessage">Type to deserialize to</typeparam>
    /// <param name="json">JSON string</param>
    /// <returns>Deserialized object</returns>
    TMessage Deserialize<TMessage>(string json);

    /// <summary>
    /// Deserializes a JSON string to a JsonRpcRequestMessage record
    /// </summary>
    /// <param name="json">JSON string to deserialize</param>
    /// <returns>Immutable JsonRpcRequestMessage record</returns>
    JsonRpcRequestMessage DeserializeRequest(string json);

    /// <summary>
    /// Deserializes a JSON string to a JsonRpcResponseMessage record
    /// </summary>
    /// <param name="json">JSON string to deserialize</param>
    /// <returns>Immutable JsonRpcResponseMessage record</returns>
    JsonRpcResponseMessage DeserializeResponse(string json);

    /// <summary>
    /// Deserializes a JSON string to a JsonRpcNotificationMessage record
    /// </summary>
    /// <param name="json">JSON string to deserialize</param>
    /// <returns>Immutable JsonRpcNotificationMessage record</returns>
    JsonRpcNotificationMessage DeserializeNotification(string json);
}
