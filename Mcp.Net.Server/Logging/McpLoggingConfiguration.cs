using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace Mcp.Net.Server.Logging
{
    /// <summary>
    /// Interface for configuring the MCP Server logging system
    /// </summary>
    public interface IMcpLoggingConfiguration
    {
        /// <summary>
        /// Creates a logger factory based on the current configuration
        /// </summary>
        /// <returns>A logger factory instance</returns>
        ILoggerFactory CreateLoggerFactory();
    }

    /// <summary>
    /// Implementation of IMcpLoggingConfiguration using Serilog
    /// </summary>
    public class McpLoggingConfiguration : IMcpLoggingConfiguration
    {
        private readonly McpLoggingOptions _options;
        private Serilog.Core.Logger? _serilogLogger;
        private SerilogLoggerFactory? _loggerFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="McpLoggingConfiguration"/> class
        /// </summary>
        /// <param name="options">The options for configuring logging</param>
        public McpLoggingConfiguration(IOptions<McpLoggingOptions> options)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
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
            loggerConfig = loggerConfig.WriteTo.File(
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

            // Set up Serilog.Log to use our logger (legacy support)
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
}