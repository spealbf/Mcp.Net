using System.Text.Json;
using Mcp.Net.Core.JsonRpc;

namespace Mcp.Net.Server.Logging
{
    /// <summary>
    /// Extension methods for standardized JSON-RPC message logging
    /// </summary>
    public static class JsonRpcLoggingExtensions
    {
        private const int MaxPayloadLogSize = 2000;

        /// <summary>
        /// Logs a JSON-RPC request with standardized format
        /// </summary>
        /// <param name="logger">The logger to use</param>
        /// <param name="requestMessage">The JSON-RPC request message</param>
        /// <param name="connectionId">Optional connection ID</param>
        public static void LogJsonRpcRequest(
            this ILogger logger,
            JsonRpcRequestMessage requestMessage,
            string? connectionId = null
        )
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (requestMessage == null)
                throw new ArgumentNullException(nameof(requestMessage));

            var paramsSummary = GetParametersSummary(requestMessage.Params);

            using (logger.BeginRequestScope(requestMessage.Id, requestMessage.Method))
            {
                if (connectionId != null)
                {
                    logger.LogInformation(
                        "JSON-RPC request received: Method={Method}, Id={RequestId}, Connection={ConnectionId}, Params={Params}",
                        requestMessage.Method,
                        requestMessage.Id,
                        connectionId,
                        paramsSummary
                    );
                }
                else
                {
                    logger.LogInformation(
                        "JSON-RPC request received: Method={Method}, Id={RequestId}, Params={Params}",
                        requestMessage.Method,
                        requestMessage.Id,
                        paramsSummary
                    );
                }
            }
        }

        /// <summary>
        /// Logs a JSON-RPC notification with standardized format
        /// </summary>
        /// <param name="logger">The logger to use</param>
        /// <param name="notificationMessage">The JSON-RPC notification message</param>
        /// <param name="connectionId">Optional connection ID</param>
        public static void LogJsonRpcNotification(
            this ILogger logger,
            JsonRpcNotificationMessage notificationMessage,
            string? connectionId = null
        )
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (notificationMessage == null)
                throw new ArgumentNullException(nameof(notificationMessage));

            var paramsSummary = GetParametersSummary(notificationMessage.Params);

            using (
                logger.BeginScope(
                    new Dictionary<string, object>
                    {
                        ["Method"] = notificationMessage.Method,
                        ["ConnectionId"] = connectionId ?? "unknown",
                    }
                )
            )
            {
                if (connectionId != null)
                {
                    logger.LogInformation(
                        "JSON-RPC notification received: Method={Method}, Connection={ConnectionId}, Params={Params}",
                        notificationMessage.Method,
                        connectionId,
                        paramsSummary
                    );
                }
                else
                {
                    logger.LogInformation(
                        "JSON-RPC notification received: Method={Method}, Params={Params}",
                        notificationMessage.Method,
                        paramsSummary
                    );
                }
            }
        }

        /// <summary>
        /// Logs a JSON-RPC response with standardized format
        /// </summary>
        /// <param name="logger">The logger to use</param>
        /// <param name="responseMessage">The JSON-RPC response message</param>
        /// <param name="connectionId">Optional connection ID</param>
        public static void LogJsonRpcResponse(
            this ILogger logger,
            JsonRpcResponseMessage responseMessage,
            string? connectionId = null
        )
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (responseMessage == null)
                throw new ArgumentNullException(nameof(responseMessage));

            bool hasError = responseMessage.Error != null;
            string resultSummary = hasError
                ? $"Error: {responseMessage.Error?.Code} - {responseMessage.Error?.Message}"
                : GetResultSummary(responseMessage.Result);

            using (
                logger.BeginScope(
                    new Dictionary<string, object>
                    {
                        ["ResponseId"] = responseMessage.Id,
                        ["HasError"] = hasError,
                        ["ConnectionId"] = connectionId ?? "unknown",
                    }
                )
            )
            {
                if (hasError)
                {
                    if (connectionId != null)
                    {
                        logger.LogWarning(
                            "JSON-RPC error response sent: Id={ResponseId}, Connection={ConnectionId}, Error={Error}",
                            responseMessage.Id,
                            connectionId,
                            resultSummary
                        );
                    }
                    else
                    {
                        logger.LogWarning(
                            "JSON-RPC error response sent: Id={ResponseId}, Error={Error}",
                            responseMessage.Id,
                            resultSummary
                        );
                    }
                }
                else
                {
                    if (connectionId != null)
                    {
                        logger.LogInformation(
                            "JSON-RPC success response sent: Id={ResponseId}, Connection={ConnectionId}, Result={Result}",
                            responseMessage.Id,
                            connectionId,
                            resultSummary
                        );
                    }
                    else
                    {
                        logger.LogInformation(
                            "JSON-RPC success response sent: Id={ResponseId}, Result={Result}",
                            responseMessage.Id,
                            resultSummary
                        );
                    }
                }
            }
        }

        /// <summary>
        /// Logs a JSON-RPC parse error with standardized format
        /// </summary>
        /// <param name="logger">The logger to use</param>
        /// <param name="exception">The exception to log</param>
        /// <param name="rawMessage">The raw message that failed to parse</param>
        /// <param name="connectionId">Optional connection ID</param>
        public static void LogJsonRpcParseError(
            this ILogger logger,
            Exception exception,
            string rawMessage,
            string? connectionId = null
        )
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            // Truncate the raw message if it's too long
            string truncatedMessage = rawMessage;
            if (truncatedMessage.Length > MaxPayloadLogSize)
            {
                truncatedMessage =
                    truncatedMessage.Substring(0, MaxPayloadLogSize) + "... [truncated]";
            }

            using (
                logger.BeginScope(
                    new Dictionary<string, object>
                    {
                        ["ExceptionType"] = exception.GetType().Name,
                        ["ConnectionId"] = connectionId ?? "unknown",
                    }
                )
            )
            {
                if (connectionId != null)
                {
                    logger.LogError(
                        exception,
                        "Failed to parse JSON-RPC message from connection {ConnectionId}: {Message}",
                        connectionId,
                        truncatedMessage
                    );
                }
                else
                {
                    logger.LogError(
                        exception,
                        "Failed to parse JSON-RPC message: {Message}",
                        truncatedMessage
                    );
                }
            }
        }

        /// <summary>
        /// Gets a summary of parameters, truncating if too large
        /// </summary>
        /// <param name="parameters">The parameters object</param>
        /// <returns>A string representation of the parameters</returns>
        private static string GetParametersSummary(object? parameters)
        {
            if (parameters == null)
            {
                return "null";
            }

            try
            {
                var json = JsonSerializer.Serialize(parameters);
                if (json.Length > MaxPayloadLogSize)
                {
                    return json.Substring(0, MaxPayloadLogSize) + "... [truncated]";
                }
                return json;
            }
            catch (Exception)
            {
                return $"<unable to serialize {parameters.GetType().Name}>";
            }
        }

        /// <summary>
        /// Gets a summary of the result, truncating if too large
        /// </summary>
        /// <param name="result">The result object</param>
        /// <returns>A string representation of the result</returns>
        private static string GetResultSummary(object? result)
        {
            if (result == null)
            {
                return "null";
            }

            try
            {
                var json = JsonSerializer.Serialize(result);
                if (json.Length > MaxPayloadLogSize)
                {
                    return json.Substring(0, MaxPayloadLogSize) + "... [truncated]";
                }
                return json;
            }
            catch (Exception)
            {
                return $"<unable to serialize {result.GetType().Name}>";
            }
        }
    }
}
