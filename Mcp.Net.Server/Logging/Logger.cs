using System.Text.Json;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace Mcp.Net.Server.Logging
{
    /// <summary>
    /// Interface for configuring the MCP Server logging system
    /// </summary>
    public interface IMcpLoggerConfiguration
    {
        /// <summary>
        /// Configure the logger with specific options
        /// </summary>
        /// <param name="loggerOptions">The options to configure the logger with</param>
        void Configure(McpLoggerOptions loggerOptions);

        /// <summary>
        /// Gets the current logging configuration
        /// </summary>
        McpLoggerOptions Options { get; }

        /// <summary>
        /// Creates a logger factory based on the current configuration
        /// </summary>
        /// <returns>A logger factory instance</returns>
        ILoggerFactory CreateLoggerFactory();
    }

    /// <summary>
    /// Logging options for the MCP Server
    /// </summary>
    public class McpLoggerOptions
    {
        /// <summary>
        /// Gets or sets whether the server is using stdio for transport
        /// </summary>
        public bool UseStdio { get; set; }

        /// <summary>
        /// Gets or sets the minimum log level
        /// </summary>
        public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;

        /// <summary>
        /// Gets or sets the path to the log file
        /// </summary>
        public string LogFilePath { get; set; } = "mcp-server.log";

        /// <summary>
        /// Gets or sets whether to disable console output
        /// </summary>
        public bool NoConsoleOutput { get; set; }

        /// <summary>
        /// Gets or sets the log file rolling interval
        /// </summary>
        public RollingInterval FileRollingInterval { get; set; } = RollingInterval.Day;

        /// <summary>
        /// Gets or sets the maximum log file size in bytes
        /// </summary>
        public long FileSizeLimitBytes { get; set; } = 10 * 1024 * 1024; // 10MB

        /// <summary>
        /// Gets or sets the maximum number of log files to retain
        /// </summary>
        public int RetainedFileCountLimit { get; set; } = 31;

        /// <summary>
        /// Gets or sets whether to format console output for better readability
        /// </summary>
        public bool PrettyConsoleOutput { get; set; } = true;

        /// <summary>
        /// Gets or sets category-specific log levels
        /// </summary>
        public Dictionary<string, LogLevel> CategoryLogLevels { get; set; } = new();
    }

    /// <summary>
    /// Implementation of IMcpLoggerConfiguration using Serilog
    /// </summary>
    public class McpLoggerConfiguration : IMcpLoggerConfiguration
    {
        private static McpLoggerConfiguration? _instance;
        private Serilog.Core.Logger? _serilogLogger;
        private SerilogLoggerFactory? _loggerFactory;
        private McpLoggerOptions _options;

        private McpLoggerConfiguration()
        {
            _options = new McpLoggerOptions();
        }

        /// <summary>
        /// Gets the singleton instance of the logger configuration
        /// </summary>
        public static IMcpLoggerConfiguration Instance =>
            _instance ??= new McpLoggerConfiguration();

        /// <summary>
        /// Gets the current logger options
        /// </summary>
        public McpLoggerOptions Options => _options;

        /// <summary>
        /// Configures the logger with specific options
        /// </summary>
        /// <param name="options">The options to configure the logger with</param>
        public void Configure(McpLoggerOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            ConfigureSerilog();
        }

        /// <summary>
        /// Creates a logger factory based on the current configuration
        /// </summary>
        /// <returns>A logger factory instance</returns>
        public ILoggerFactory CreateLoggerFactory()
        {
            if (_serilogLogger == null)
            {
                ConfigureSerilog();
            }

            // Dispose previous factory if it exists
            _loggerFactory?.Dispose();
            _loggerFactory = new SerilogLoggerFactory(_serilogLogger, true);

            return _loggerFactory;
        }

        private void ConfigureSerilog()
        {
            // Convert minimum log level to Serilog equivalent
            var minimumLevel = ConvertLogLevel(_options.MinimumLogLevel);

            var loggerConfig = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Is(minimumLevel);

            // Configure file logging
            var fileConfig = loggerConfig.WriteTo.File(
                path: _options.LogFilePath,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}",
                rollingInterval: _options.FileRollingInterval,
                fileSizeLimitBytes: _options.FileSizeLimitBytes,
                retainedFileCountLimit: _options.RetainedFileCountLimit
            );

            // Only add console logging if not in stdio mode and NoConsoleOutput is not set
            if (!_options.UseStdio && !_options.NoConsoleOutput)
            {
                if (_options.PrettyConsoleOutput)
                {
                    loggerConfig = loggerConfig.WriteTo.Console(
                        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
                    );
                }
                else
                {
                    loggerConfig = loggerConfig.WriteTo.Console(
                        outputTemplate: "{Message:lj}{NewLine}{Exception}"
                    );
                }
            }

            // Apply category-specific log levels
            foreach (var categoryLevel in _options.CategoryLogLevels)
            {
                var logEventLevel = ConvertLogLevel(categoryLevel.Value);
                loggerConfig.MinimumLevel.Override(categoryLevel.Key, logEventLevel);
            }

            // Create the Serilog logger
            _serilogLogger = loggerConfig.CreateLogger();

            // Set up Serilog.Log to use our logger
            Log.Logger = _serilogLogger;
        }

        private static LogEventLevel ConvertLogLevel(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Trace => LogEventLevel.Verbose,
                LogLevel.Debug => LogEventLevel.Debug,
                LogLevel.Information => LogEventLevel.Information,
                LogLevel.Warning => LogEventLevel.Warning,
                LogLevel.Error => LogEventLevel.Error,
                LogLevel.Critical => LogEventLevel.Fatal,
                _ => LogEventLevel.Information,
            };
        }
    }

    /// <summary>
    /// Static logger helper for backward compatibility
    /// </summary>
    public static class Logger
    {
        private static Microsoft.Extensions.Logging.ILogger? _staticLogger;

        /// <summary>
        /// Sets up the static logger instance
        /// </summary>
        /// <param name="logger">The logger to use for static logging</param>
        public static void SetupStaticLogger(Microsoft.Extensions.Logging.ILogger logger)
        {
            _staticLogger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // Static compatibility methods

        /// <summary>
        /// Initializes the logger with specific options
        /// </summary>
        /// <param name="options">The options to configure the logger with</param>
        public static void Initialize(LoggerOptions options)
        {
            // Convert old options to new options
            var newOptions = new McpLoggerOptions
            {
                UseStdio = options.UseStdio,
                MinimumLogLevel = options.DebugMode ? LogLevel.Debug : LogLevel.Information,
                LogFilePath = options.LogFilePath,
                NoConsoleOutput = options.NoConsoleOutput,
            };

            // Configure the new logger
            McpLoggerConfiguration.Instance.Configure(newOptions);
        }

        /// <summary>
        /// Creates a scope for correlating log messages
        /// </summary>
        /// <param name="sessionId">The session ID to include in the scope</param>
        /// <returns>A disposable scope</returns>
        public static IDisposable BeginScope(string sessionId)
        {
            return Serilog.Context.LogContext.PushProperty("SessionId", sessionId);
        }

        // Debug logging
        public static void Debug(string message, params object[] propertyValues)
        {
            Log.Debug(message, propertyValues);
        }

        // Information logging
        public static void Information(string message, params object[] propertyValues)
        {
            Log.Information(message, propertyValues);
        }

        // Warning logging
        public static void Warning(string message, params object[] propertyValues)
        {
            Log.Warning(message, propertyValues);
        }

        // Error logging
        public static void Error(string message, Exception? ex = null)
        {
            if (ex != null)
            {
                Log.Error(ex, message);
            }
            else
            {
                Log.Error(message);
            }
        }

        // Error logging with context
        public static void Error(string message, Exception ex, string contextValue)
        {
            Log.Error(ex, message + " {ContextValue}", contextValue);
        }

        // JSON-RPC request logging
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

        // JSON-RPC response logging
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

        // Tool call logging
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
    }

    /// <summary>
    /// Legacy logger options for backward compatibility
    /// </summary>
    public class LoggerOptions
    {
        public bool UseStdio { get; set; }
        public bool DebugMode { get; set; }
        public string LogFilePath { get; set; } = "mcp-server.log";
        public bool NoConsoleOutput { get; set; }
    }
}
