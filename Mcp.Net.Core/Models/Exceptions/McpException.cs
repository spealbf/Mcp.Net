using System;

namespace Mcp.Net.Core.Models.Exceptions
{
    /// <summary>
    /// Standard JSON-RPC error codes defined by the MCP protocol
    /// </summary>
    public enum ErrorCode
    {
        // SDK error codes
        ConnectionClosed = -32000,
        RequestTimeout = -32001,

        // Standard JSON-RPC error codes
        ParseError = -32700,
        InvalidRequest = -32600,
        MethodNotFound = -32601,
        InvalidParams = -32602,
        InternalError = -32603,

        // Resource-specific error codes
        ResourceNotFound = -32801,
        ResourceAccessDenied = -32802,

        // Prompt-specific error codes
        PromptNotFound = -32901,
        PromptExecutionFailed = -32902,
    }

    /// <summary>
    /// Exception type for MCP protocol errors with standard error codes.
    /// </summary>
    public class McpException : Exception
    {
        /// <summary>
        /// Gets the error code associated with this exception.
        /// </summary>
        public ErrorCode Code { get; }

        /// <summary>
        /// Gets optional additional error data about the exception.
        /// </summary>
        public object? ErrorData { get; }

        /// <summary>
        /// Creates a new instance of the <see cref="McpException"/> class with the specified error code and message.
        /// </summary>
        /// <param name="code">The error code.</param>
        /// <param name="message">The error message.</param>
        public McpException(ErrorCode code, string message)
            : base(message)
        {
            Code = code;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="McpException"/> class with the specified error code, message, and additional data.
        /// </summary>
        /// <param name="code">The error code.</param>
        /// <param name="message">The error message.</param>
        /// <param name="errorData">Additional data about the error.</param>
        public McpException(ErrorCode code, string message, object? errorData)
            : base(message)
        {
            Code = code;
            ErrorData = errorData;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="McpException"/> class with the specified error code, message, and inner exception.
        /// </summary>
        /// <param name="code">The error code.</param>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public McpException(ErrorCode code, string message, Exception innerException)
            : base(message, innerException)
        {
            Code = code;
        }
    }
}
