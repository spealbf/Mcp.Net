namespace Mcp.Net.Server.Logging
{
    /// <summary>
    /// Extension methods for logging message parsing operations
    /// </summary>
    public static class MessageParserLoggingExtensions
    {
        /// <summary>
        /// Logs a message parsing attempt
        /// </summary>
        /// <param name="logger">The logger to use</param>
        /// <param name="input">The input being parsed (truncated if too long)</param>
        /// <param name="success">Whether parsing was successful</param>
        /// <param name="messageType">Type of message if successfully parsed</param>
        public static void LogMessageParse(
            this ILogger logger,
            string input,
            bool success,
            string? messageType = null
        )
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            // Truncate input if too long
            const int maxInputLogLength = 200;
            string truncatedInput =
                input.Length > maxInputLogLength
                    ? input.Substring(0, maxInputLogLength) + "... [truncated]"
                    : input;

            if (success)
            {
                logger.LogDebug(
                    "Successfully parsed message of type {MessageType}: {TruncatedInput}",
                    messageType ?? "unknown",
                    truncatedInput
                );
            }
            else
            {
                logger.LogWarning("Failed to parse message: {TruncatedInput}", truncatedInput);
            }
        }

        /// <summary>
        /// Logs JSON-RPC message validation
        /// </summary>
        /// <param name="logger">The logger to use</param>
        /// <param name="message">The message being validated</param>
        /// <param name="isValid">Whether the message is valid</param>
        /// <param name="messageType">Type of message being validated</param>
        /// <param name="validationIssue">Issue description if not valid</param>
        public static void LogMessageValidation(
            this ILogger logger,
            string message,
            bool isValid,
            string messageType,
            string? validationIssue = null
        )
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            // Truncate message if too long
            const int maxMessageLogLength = 200;
            string truncatedMessage =
                message.Length > maxMessageLogLength
                    ? message.Substring(0, maxMessageLogLength) + "... [truncated]"
                    : message;

            if (isValid)
            {
                logger.LogDebug(
                    "Successfully validated {MessageType} message: {TruncatedMessage}",
                    messageType,
                    truncatedMessage
                );
            }
            else
            {
                logger.LogWarning(
                    "Invalid {MessageType} message: {ValidationIssue}. Message: {TruncatedMessage}",
                    messageType,
                    validationIssue ?? "Unknown validation issue",
                    truncatedMessage
                );
            }
        }

        /// <summary>
        /// Logs deserialization errors
        /// </summary>
        /// <param name="logger">The logger to use</param>
        /// <param name="exception">The exception that occurred</param>
        /// <param name="targetType">The type the message was being deserialized to</param>
        /// <param name="message">The message that failed to deserialize</param>
        public static void LogDeserializationError(
            this ILogger logger,
            Exception exception,
            string targetType,
            string message
        )
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            // Truncate message if too long
            const int maxMessageLogLength = 500;
            string truncatedMessage =
                message.Length > maxMessageLogLength
                    ? message.Substring(0, maxMessageLogLength) + "... [truncated]"
                    : message;

            using (
                logger.BeginScope(
                    new Dictionary<string, object>
                    {
                        ["TargetType"] = targetType,
                        ["ExceptionType"] = exception.GetType().Name,
                        ["ExceptionMessage"] = exception.Message,
                    }
                )
            )
            {
                logger.LogError(
                    exception,
                    "Failed to deserialize message to {TargetType}: {Message}",
                    targetType,
                    truncatedMessage
                );
            }
        }
    }
}
