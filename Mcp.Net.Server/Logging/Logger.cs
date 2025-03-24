using System.Text.Json;
using Serilog;
using Serilog.Events;

namespace Mcp.Net.Server.Logging
{
    public static class Logger
    {
        private static Serilog.ILogger? _logger;
        private static bool _useStdio;
        private static bool _debugMode;
        private static bool _noConsoleOutput;

        public static void Initialize(LoggerOptions options)
        {
            _useStdio = options.UseStdio;
            _debugMode = options.DebugMode;
            _noConsoleOutput = options.NoConsoleOutput;

            var loggerConfig = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Is(
                    options.DebugMode ? LogEventLevel.Debug : LogEventLevel.Information
                );

            // Always write to file
            loggerConfig = loggerConfig.WriteTo.File(
                options.LogFilePath,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
            );

            // Only add console logging if not in stdio mode and NoConsoleOutput is not set
            if (!options.UseStdio && !options.NoConsoleOutput)
            {
                loggerConfig = loggerConfig.WriteTo.Console(
                    outputTemplate: "[{Level:u3}] {Message:lj}{NewLine}{Exception}"
                );
            }

            _logger = loggerConfig.CreateLogger();
        }

        public static void Debug(string message, params object[] propertyValues)
        {
            if (_debugMode)
            {
                _logger?.Debug(message, propertyValues);
            }
        }

        public static void Information(string message, params object[] propertyValues)
        {
            _logger?.Information(message, propertyValues);

            // If not in stdio mode and console output is allowed, we can also output to console directly for immediate feedback
            if (!_useStdio && !_noConsoleOutput && _logger == null)
            {
                Console.WriteLine($"INFO: {message}");
            }
        }

        public static void Warning(string message, params object[] propertyValues)
        {
            _logger?.Warning(message, propertyValues);

            // If not in stdio mode and console output is allowed, we can also output to console directly for immediate feedback
            if (!_useStdio && !_noConsoleOutput && _logger == null)
            {
                Console.WriteLine($"WARNING: {message}");
            }
        }

        public static void Error(string message, Exception? ex = null)
        {
            if (ex != null)
            {
                _logger?.Error(ex, message);

                // If not in stdio mode and console output is allowed, we can also output to console directly for immediate feedback
                if (!_useStdio && !_noConsoleOutput && _logger == null)
                {
                    Console.Error.WriteLine($"ERROR: {message} - {ex.Message}");
                }
            }
            else
            {
                _logger?.Error(message);

                // If not in stdio mode and console output is allowed, we can also output to console directly for immediate feedback
                if (!_useStdio && !_noConsoleOutput && _logger == null)
                {
                    Console.Error.WriteLine($"ERROR: {message}");
                }
            }
        }

        public static void Error(string message, Exception ex, string contextValue)
        {
            _logger?.Error(ex, message + " {ContextValue}", contextValue);

            // If not in stdio mode and console output is allowed, we can also output to console directly for immediate feedback
            if (!_useStdio && !_noConsoleOutput && _logger == null)
            {
                Console.Error.WriteLine(
                    $"ERROR: {message} - {ex.Message} - Context: {contextValue}"
                );
            }
        }

        public static void LogRequest(
            string sessionId,
            string method,
            string? id = null,
            object? parameters = null
        )
        {
            Debug(
                "JSON-RPC Request: Session={SessionId}, Id={Id}, Method={Method}, Parameters={Parameters}",
                sessionId,
                id ?? "null",
                method,
                parameters != null ? JsonSerializer.Serialize(parameters) : "null"
            );
        }

        public static void LogResponse(
            string sessionId,
            string? id,
            bool isError,
            object? result = null,
            object? error = null
        )
        {
            Debug(
                "JSON-RPC Response: Session={SessionId}, Id={Id}, IsError={IsError}, Result={Result}, Error={Error}",
                sessionId,
                id ?? "null",
                isError,
                result != null ? JsonSerializer.Serialize(result) : "null",
                error != null ? JsonSerializer.Serialize(error) : "null"
            );
        }

        public static void LogToolCall(
            string sessionId,
            string toolName,
            bool isSuccess,
            string? errorMessage = null
        )
        {
            if (isSuccess)
            {
                Information(
                    "Tool Call: Session={SessionId}, Tool={ToolName}, Status=Success",
                    sessionId,
                    toolName
                );
            }
            else
            {
                Warning(
                    "Tool Call: Session={SessionId}, Tool={ToolName}, Status=Error, Message={ErrorMessage}",
                    sessionId,
                    toolName,
                    errorMessage ?? "Unknown error"
                );
            }
        }

        public static IDisposable BeginScope(string sessionId)
        {
            return Serilog.Context.LogContext.PushProperty("SessionId", sessionId);
        }
    }

    public class LoggerOptions
    {
        public bool UseStdio { get; set; }
        public bool DebugMode { get; set; }
        public string LogFilePath { get; set; } = "mcp-server.log";
        public bool NoConsoleOutput { get; set; }
    }
}
