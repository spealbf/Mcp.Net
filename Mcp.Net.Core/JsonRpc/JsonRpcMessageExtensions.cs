using Mcp.Net.Core.Models.Exceptions;

namespace Mcp.Net.Core.JsonRpc;

/// <summary>
/// Extension methods for JSON-RPC message classes
/// </summary>
public static class JsonRpcMessageExtensions
{
    /// <summary>
    /// Creates an error response message for a JSON-RPC request
    /// </summary>
    /// <param name="id">The request ID</param>
    /// <param name="code">The error code</param>
    /// <param name="message">The error message</param>
    /// <param name="data">Optional error data</param>
    /// <returns>A JSON-RPC response message with error information</returns>
    public static JsonRpcResponseMessage CreateErrorResponse(
        string id,
        ErrorCode code,
        string message,
        object? data = null
    )
    {
        var error = new JsonRpcError
        {
            Code = (int)code,
            Message = message,
            Data = data,
        };
        return new JsonRpcResponseMessage("2.0", id, null, error);
    }
}