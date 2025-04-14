namespace Mcp.Net.Server.Logging
{
    /// <summary>
    /// Extension methods for standardized transport logging
    /// </summary>
    public static class TransportLoggingExtensions
    {
        /// <summary>
        /// Logs a transport message send event with standardized format
        /// </summary>
        /// <param name="logger">The logger to use</param>
        /// <param name="transportId">Transport identifier (connection/session ID)</param>
        /// <param name="messageType">Type of message being sent</param>
        /// <param name="messageId">Message identifier</param>
        /// <param name="payloadSize">Size of the message payload in bytes</param>
        /// <param name="transportType">Type of transport (SSE, WebSocket, etc.)</param>
        public static void LogMessageSent(
            this ILogger logger,
            string transportId,
            string messageType,
            string? messageId,
            int payloadSize,
            string transportType
        )
        {
            logger.LogDebug(
                "{TransportType} transport {TransportId} sent {MessageType} message: ID={MessageId}, Size={PayloadSizeBytes}B",
                transportType,
                transportId,
                messageType,
                messageId ?? "null",
                payloadSize
            );
        }

        /// <summary>
        /// Logs a transport message receive event with standardized format
        /// </summary>
        /// <param name="logger">The logger to use</param>
        /// <param name="transportId">Transport identifier (connection/session ID)</param>
        /// <param name="messageType">Type of message received</param>
        /// <param name="messageId">Message identifier</param>
        /// <param name="payloadSize">Size of the message payload in bytes</param>
        /// <param name="transportType">Type of transport (SSE, WebSocket, etc.)</param>
        public static void LogMessageReceived(
            this ILogger logger,
            string transportId,
            string messageType,
            string? messageId,
            int payloadSize,
            string transportType
        )
        {
            logger.LogDebug(
                "{TransportType} transport {TransportId} received {MessageType} message: ID={MessageId}, Size={PayloadSizeBytes}B",
                transportType,
                transportId,
                messageType,
                messageId ?? "null",
                payloadSize
            );
        }

        /// <summary>
        /// Logs a transport connection event with standardized format
        /// </summary>
        /// <param name="logger">The logger to use</param>
        /// <param name="transportId">Transport identifier (connection/session ID)</param>
        /// <param name="clientInfo">Client information (IP, user agent, etc.)</param>
        /// <param name="transportType">Type of transport (SSE, WebSocket, etc.)</param>
        /// <param name="isConnected">True if connected, false if disconnected</param>
        public static void LogConnectionEvent(
            this ILogger logger,
            string transportId,
            Dictionary<string, string?> clientInfo,
            string transportType,
            bool isConnected
        )
        {
            // Format the client information
            var clientInfoStr = string.Join(
                ", ",
                clientInfo
                    .Where(kv => !string.IsNullOrEmpty(kv.Value))
                    .Select(kv => $"{kv.Key}={kv.Value}")
            );

            if (isConnected)
            {
                logger.LogInformation(
                    "{TransportType} transport {TransportId} connected: {ClientInfo}",
                    transportType,
                    transportId,
                    clientInfoStr
                );
            }
            else
            {
                logger.LogInformation(
                    "{TransportType} transport {TransportId} disconnected: {ClientInfo}",
                    transportType,
                    transportId,
                    clientInfoStr
                );
            }
        }

        /// <summary>
        /// Logs a transport error with standardized format
        /// </summary>
        /// <param name="logger">The logger to use</param>
        /// <param name="exception">The exception to log</param>
        /// <param name="transportId">Transport identifier (connection/session ID)</param>
        /// <param name="operation">The operation that failed</param>
        /// <param name="transportType">Type of transport (SSE, WebSocket, etc.)</param>
        /// <param name="messageId">Optional message ID associated with the error</param>
        public static void LogTransportError(
            this ILogger logger,
            Exception exception,
            string transportId,
            string operation,
            string transportType,
            string? messageId = null
        )
        {
            var scopeData = new Dictionary<string, object>
            {
                ["TransportId"] = transportId,
                ["TransportType"] = transportType,
                ["Operation"] = operation,
                ["ExceptionType"] = exception.GetType().Name,
            };

            if (messageId != null)
            {
                scopeData["MessageId"] = messageId;
            }

            using (logger.BeginScope(scopeData))
            {
                logger.LogError(
                    exception,
                    "{TransportType} transport {TransportId} error during {Operation}{MessageContext}",
                    transportType,
                    transportId,
                    operation,
                    messageId != null ? $" for message {messageId}" : string.Empty
                );
            }
        }

        /// <summary>
        /// Logs detailed transport metrics with standardized format
        /// </summary>
        /// <param name="logger">The logger to use</param>
        /// <param name="transportId">Transport identifier (connection/session ID)</param>
        /// <param name="metrics">Dictionary of metrics to log</param>
        /// <param name="transportType">Type of transport (SSE, WebSocket, etc.)</param>
        public static void LogTransportMetrics(
            this ILogger logger,
            string transportId,
            Dictionary<string, object> metrics,
            string transportType
        )
        {
            using (
                logger.BeginScope(
                    new Dictionary<string, object>
                    {
                        ["TransportId"] = transportId,
                        ["TransportType"] = transportType,
                    }
                )
            )
            {
                // Add all metrics to the log context
                using (logger.BeginScope(metrics))
                {
                    logger.LogInformation(
                        "{TransportType} transport {TransportId} metrics: {MetricsDetail}",
                        transportType,
                        transportId,
                        string.Join(", ", metrics.Select(kv => $"{kv.Key}={kv.Value}"))
                    );
                }
            }
        }
    }
}
