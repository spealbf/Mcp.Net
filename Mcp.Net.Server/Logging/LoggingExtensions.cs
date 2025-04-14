using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.Server.Logging
{
    /// <summary>
    /// Extension methods to standardize logging patterns throughout the MCP Server
    /// </summary>
    public static class LoggingExtensions
    {
        /// <summary>
        /// Creates a logging scope for processing a request
        /// </summary>
        /// <typeparam name="T">Type of request ID</typeparam>
        /// <param name="logger">The logger to create the scope for</param>
        /// <param name="requestId">The request ID</param>
        /// <param name="method">The method being called</param>
        /// <returns>A disposable scope</returns>
        [return: NotNull]
        public static IDisposable BeginRequestScope<T>(
            this ILogger logger,
            T requestId,
            string method
        )
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            return logger.BeginScope(
                    new Dictionary<string, object>
                    {
                        ["RequestId"] = requestId?.ToString() ?? "null",
                        ["Method"] = method ?? "unknown",
                    }
                ) ?? new NoOpDisposable();
        }

        /// <summary>
        /// Creates a logging scope for a tool execution
        /// </summary>
        /// <param name="logger">The logger to create the scope for</param>
        /// <param name="toolName">The name of the tool being executed</param>
        /// <param name="requestId">Optional request ID associated with the tool execution</param>
        /// <returns>A disposable scope</returns>
        [return: NotNull]
        public static IDisposable BeginToolScope<T>(
            this ILogger logger,
            string toolName,
            T? requestId = default
        )
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            var scopeData = new Dictionary<string, object> { ["ToolName"] = toolName ?? "unknown" };

            if (requestId != null)
            {
                scopeData["RequestId"] = requestId.ToString() ?? "null";
            }

            return logger.BeginScope(scopeData) ?? new NoOpDisposable();
        }

        /// <summary>
        /// Creates a logging scope for a connection
        /// </summary>
        /// <param name="logger">The logger to create the scope for</param>
        /// <param name="connectionId">The connection ID</param>
        /// <param name="clientIp">The client IP address</param>
        /// <returns>A disposable scope</returns>
        [return: NotNull]
        public static IDisposable BeginConnectionScope(
            this ILogger logger,
            string connectionId,
            string? clientIp = null
        )
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            var scopeData = new Dictionary<string, object>
            {
                ["ConnectionId"] = connectionId ?? "unknown",
            };

            if (!string.IsNullOrEmpty(clientIp))
            {
                scopeData["ClientIp"] = clientIp;
            }

            return logger.BeginScope(scopeData) ?? new NoOpDisposable();
        }

        /// <summary>
        /// Creates a performance timing scope that logs execution time on disposal
        /// </summary>
        /// <param name="logger">The logger to create the scope for</param>
        /// <param name="operationName">The name of the operation being timed</param>
        /// <param name="logLevel">The log level for the timing message</param>
        /// <returns>A disposable timing scope</returns>
        [return: NotNull]
        public static IDisposable BeginTimingScope(
            this ILogger logger,
            string operationName,
            LogLevel logLevel = LogLevel.Debug
        )
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            return new TimingScope(logger, operationName, logLevel);
        }

        /// <summary>
        /// Empty IDisposable implementation for when BeginScope returns null
        /// </summary>
        private class NoOpDisposable : IDisposable
        {
            public void Dispose() { }
        }

        /// <summary>
        /// Helper class to measure and log execution time of operations
        /// </summary>
        private class TimingScope : IDisposable
        {
            private readonly ILogger _logger;
            private readonly string _operationName;
            private readonly LogLevel _logLevel;
            private readonly Stopwatch _stopwatch;

            public TimingScope(ILogger logger, string operationName, LogLevel logLevel)
            {
                _logger = logger;
                _operationName = operationName;
                _logLevel = logLevel;
                _stopwatch = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                _stopwatch.Stop();
                _logger.Log(
                    _logLevel,
                    "Operation {OperationName} completed in {ExecutionTimeMs}ms",
                    _operationName,
                    _stopwatch.ElapsedMilliseconds
                );
            }
        }
    }
}
