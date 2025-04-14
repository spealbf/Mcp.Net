using System.Collections;
using System.Diagnostics;
using System.Text.Json;

namespace Mcp.Net.Server.Logging
{
    /// <summary>
    /// Extension methods for standardized exception logging
    /// </summary>
    public static class ExceptionLoggingExtensions
    {
        private const int MaxPayloadLogSize = 2000;

        /// <summary>
        /// Logs a tool execution exception with standardized format and rich context
        /// </summary>
        /// <param name="logger">The logger to use</param>
        /// <param name="exception">The exception to log</param>
        /// <param name="toolName">The name of the tool that threw the exception</param>
        /// <param name="parameters">Optional tool parameters (will be serialized to JSON)</param>
        /// <param name="requestId">Optional request ID associated with the tool execution</param>
        public static void LogToolException(
            this ILogger logger,
            Exception exception,
            string toolName,
            object? parameters = null,
            string? requestId = null
        )
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            // Extract rich exception details
            var exceptionContext = GetExceptionContext(exception);

            // Build scope dictionary with all context
            var scopeData = new Dictionary<string, object>
            {
                ["ToolName"] = toolName,
                ["ExceptionType"] = exception.GetType().Name,
                ["ExceptionSource"] =
                    exception.TargetSite?.DeclaringType?.FullName ?? exception.Source ?? "unknown",
            };

            // Add request ID if available
            if (requestId != null)
            {
                scopeData["RequestId"] = requestId;
            }

            // Add exception context data
            foreach (var kvp in exceptionContext)
            {
                scopeData[kvp.Key] = kvp.Value;
            }

            // Begin scope with all context data
            using (logger.BeginScope(scopeData))
            {
                if (parameters != null)
                {
                    // Include parameters in the log, but with a size limit to avoid huge logs
                    string paramsJson = JsonSerializer.Serialize(parameters);
                    if (paramsJson.Length > MaxPayloadLogSize)
                    {
                        paramsJson = paramsJson.Substring(0, MaxPayloadLogSize) + "... [truncated]";
                    }

                    logger.LogError(
                        exception,
                        "Tool {ToolName} execution failed with parameters: {Parameters}{RequestContext}",
                        toolName,
                        paramsJson,
                        requestId != null ? $" (Request: {requestId})" : string.Empty
                    );
                }
                else
                {
                    logger.LogError(
                        exception,
                        "Tool {ToolName} execution failed{RequestContext}",
                        toolName,
                        requestId != null ? $" (Request: {requestId})" : string.Empty
                    );
                }
            }
        }

        /// <summary>
        /// Logs a request processing exception with standardized format and rich context
        /// </summary>
        /// <typeparam name="T">Type of request ID</typeparam>
        /// <param name="logger">The logger to use</param>
        /// <param name="exception">The exception to log</param>
        /// <param name="requestId">The ID of the request that failed</param>
        /// <param name="method">The method that was called</param>
        /// <param name="connectionId">Optional connection ID associated with the request</param>
        public static void LogRequestException<T>(
            this ILogger logger,
            Exception exception,
            T requestId,
            string method,
            string? connectionId = null
        )
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            // Extract rich exception details
            var exceptionContext = GetExceptionContext(exception);

            // Build scope dictionary with all context
            var scopeData = new Dictionary<string, object>
            {
                ["RequestId"] = requestId?.ToString() ?? "unknown",
                ["Method"] = method,
                ["ExceptionType"] = exception.GetType().Name,
                ["ExceptionSource"] =
                    exception.TargetSite?.DeclaringType?.FullName ?? exception.Source ?? "unknown",
            };

            // Add connection ID if available
            if (connectionId != null)
            {
                scopeData["ConnectionId"] = connectionId;
            }

            // Add exception context data
            foreach (var kvp in exceptionContext)
            {
                scopeData[kvp.Key] = kvp.Value;
            }

            // Begin scope with all context data
            using (logger.BeginScope(scopeData))
            {
                if (connectionId != null)
                {
                    logger.LogError(
                        exception,
                        "Error processing request {RequestId} for method {Method} on connection {ConnectionId}",
                        requestId,
                        method,
                        connectionId
                    );
                }
                else
                {
                    logger.LogError(
                        exception,
                        "Error processing request {RequestId} for method {Method}",
                        requestId,
                        method
                    );
                }
            }
        }

        /// <summary>
        /// Logs a connection exception with standardized format and rich context
        /// </summary>
        /// <param name="logger">The logger to use</param>
        /// <param name="exception">The exception to log</param>
        /// <param name="connectionId">The connection ID</param>
        /// <param name="operation">The operation that failed</param>
        /// <param name="clientInfo">Optional dictionary with client information</param>
        public static void LogConnectionException(
            this ILogger logger,
            Exception exception,
            string connectionId,
            string operation,
            Dictionary<string, string?>? clientInfo = null
        )
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            // Extract rich exception details
            var exceptionContext = GetExceptionContext(exception);

            // Build scope dictionary with all context
            var scopeData = new Dictionary<string, object>
            {
                ["ConnectionId"] = connectionId,
                ["Operation"] = operation,
                ["ExceptionType"] = exception.GetType().Name,
                ["ExceptionSource"] =
                    exception.TargetSite?.DeclaringType?.FullName ?? exception.Source ?? "unknown",
            };

            // Add client info if available
            if (clientInfo != null)
            {
                foreach (var kvp in clientInfo.Where(kv => !string.IsNullOrEmpty(kv.Value)))
                {
                    scopeData[$"Client{kvp.Key}"] = kvp.Value!;
                }
            }

            // Add exception context data
            foreach (var kvp in exceptionContext)
            {
                scopeData[kvp.Key] = kvp.Value;
            }

            // Begin scope with all context data
            using (logger.BeginScope(scopeData))
            {
                if (clientInfo != null && clientInfo.Count > 0)
                {
                    // Format the client information
                    var clientInfoStr = string.Join(
                        ", ",
                        clientInfo
                            .Where(kv => !string.IsNullOrEmpty(kv.Value))
                            .Select(kv => $"{kv.Key}={kv.Value}")
                    );

                    logger.LogError(
                        exception,
                        "Connection {ConnectionId} encountered an error during {Operation}. Client: {ClientInfo}",
                        connectionId,
                        operation,
                        clientInfoStr
                    );
                }
                else
                {
                    logger.LogError(
                        exception,
                        "Connection {ConnectionId} encountered an error during {Operation}",
                        connectionId,
                        operation
                    );
                }
            }
        }

        /// <summary>
        /// Logs a startup exception with standardized format and rich context
        /// </summary>
        /// <param name="logger">The logger to use</param>
        /// <param name="exception">The exception to log</param>
        /// <param name="component">The component that failed to start</param>
        /// <param name="additionalData">Optional dictionary with additional context data</param>
        public static void LogStartupException(
            this ILogger logger,
            Exception exception,
            string component,
            Dictionary<string, object>? additionalData = null
        )
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            // Extract rich exception details
            var exceptionContext = GetExceptionContext(exception);

            // Build scope dictionary with all context
            var scopeData = new Dictionary<string, object>
            {
                ["Component"] = component,
                ["ExceptionType"] = exception.GetType().Name,
                ["ExceptionSource"] =
                    exception.TargetSite?.DeclaringType?.FullName ?? exception.Source ?? "unknown",
                ["ProcessId"] = Process.GetCurrentProcess().Id,
                ["StartupTime"] = DateTimeOffset.UtcNow.ToString("o"),
            };

            // Add additional data if available
            if (additionalData != null)
            {
                foreach (var kvp in additionalData)
                {
                    scopeData[kvp.Key] = kvp.Value;
                }
            }

            // Add exception context data
            foreach (var kvp in exceptionContext)
            {
                scopeData[kvp.Key] = kvp.Value;
            }

            // Begin scope with all context data
            using (logger.BeginScope(scopeData))
            {
                logger.LogCritical(
                    exception,
                    "Fatal error during startup of {Component}",
                    component
                );
            }
        }

        /// <summary>
        /// Extracts contextual data from an exception
        /// </summary>
        /// <param name="exception">The exception to extract context from</param>
        /// <returns>Dictionary with exception context data</returns>
        private static Dictionary<string, object> GetExceptionContext(Exception exception)
        {
            var context = new Dictionary<string, object>();

            // Add basic exception details
            context["ExceptionMessage"] = exception.Message;

            // Add Data dictionary entries if any
            if (exception.Data.Count > 0)
            {
                foreach (DictionaryEntry entry in exception.Data)
                {
                    if (entry.Key is string key)
                    {
                        context[$"ExceptionData_{key}"] = entry.Value?.ToString() ?? "null";
                    }
                }
            }

            // For AggregateException, add inner exception count
            if (exception is AggregateException aggEx)
            {
                context["InnerExceptionCount"] = aggEx.InnerExceptions.Count;

                // Add first few inner exceptions
                for (int i = 0; i < Math.Min(aggEx.InnerExceptions.Count, 3); i++)
                {
                    var inner = aggEx.InnerExceptions[i];
                    context[$"InnerException_{i + 1}_Type"] = inner.GetType().Name;
                    context[$"InnerException_{i + 1}_Message"] = inner.Message;
                }
            }
            // For regular exceptions with inner exception
            else if (exception.InnerException != null)
            {
                context["InnerExceptionType"] = exception.InnerException.GetType().Name;
                context["InnerExceptionMessage"] = exception.InnerException.Message;
            }

            return context;
        }
    }
}
