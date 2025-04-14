using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Mcp.Net.Server.Options;

/// <summary>
/// Provides logging services using LoggingOptions.
/// </summary>
public interface ILoggingProvider
{
    /// <summary>
    /// Gets the logging options.
    /// </summary>
    LoggingOptions Options { get; }

    /// <summary>
    /// Creates a logger factory based on the current options.
    /// </summary>
    /// <returns>A configured logger factory</returns>
    ILoggerFactory CreateLoggerFactory();

    /// <summary>
    /// Creates a logger for a specific category.
    /// </summary>
    /// <param name="categoryName">The category name</param>
    /// <returns>A configured logger</returns>
    ILogger CreateLogger(string categoryName);
}

/// <summary>
/// Implementation of ILoggingProvider using Serilog.
/// </summary>
public class LoggingProvider : ILoggingProvider
{
    private readonly LoggingOptions _options;
    private Serilog.Core.Logger? _serilogLogger;
    private SerilogLoggerFactory? _loggerFactory;

    /// <summary>
    /// Gets the logging options.
    /// </summary>
    public LoggingOptions Options => _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingProvider"/> class.
    /// </summary>
    /// <param name="options">The options to configure logging with</param>
    public LoggingProvider(LoggingOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingProvider"/> class.
    /// </summary>
    /// <param name="optionsMonitor">Monitor for logging options</param>
    public LoggingProvider(IOptionsMonitor<LoggingOptions> optionsMonitor)
    {
        if (optionsMonitor == null)
            throw new ArgumentNullException(nameof(optionsMonitor));

        _options = optionsMonitor.CurrentValue;

        // Subscribe to options changes
        optionsMonitor.OnChange(options =>
        {
            // Update our options
            _options.MinimumLogLevel = options.MinimumLogLevel;
            _options.UseConsoleLogging = options.UseConsoleLogging;
            _options.LogFilePath = options.LogFilePath;
            _options.UseStdio = options.UseStdio;
            _options.PrettyConsoleOutput = options.PrettyConsoleOutput;
            _options.FileRollingInterval = options.FileRollingInterval;
            _options.FileSizeLimitBytes = options.FileSizeLimitBytes;
            _options.RetainedFileCountLimit = options.RetainedFileCountLimit;
            _options.ComponentLogLevels = new Dictionary<string, LogLevel>(
                options.ComponentLogLevels
            );

            // Reconfigure Serilog
            ConfigureSerilog();
        });
    }

    /// <summary>
    /// Creates a logger factory based on the current options.
    /// </summary>
    /// <returns>A configured logger factory</returns>
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

    /// <summary>
    /// Creates a logger for a specific category.
    /// </summary>
    /// <param name="categoryName">The category name</param>
    /// <returns>A configured logger</returns>
    public ILogger CreateLogger(string categoryName)
    {
        return CreateLoggerFactory().CreateLogger(categoryName);
    }

    private void ConfigureSerilog()
    {
        // Convert minimum log level to Serilog equivalent
        var minimumLevel = ConvertLogLevel(_options.MinimumLogLevel);

        var loggerConfig = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .MinimumLevel.Is(minimumLevel);

        // Configure file logging if enabled
        if (_options.UseFileLogging)
        {
            loggerConfig = loggerConfig.WriteTo.File(
                path: _options.LogFilePath ?? "logs/mcp-server.log",
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}",
                rollingInterval: _options.FileRollingInterval,
                fileSizeLimitBytes: _options.FileSizeLimitBytes,
                retainedFileCountLimit: _options.RetainedFileCountLimit
            );
        }

        // Only add console logging if not in stdio mode and UseConsoleLogging is set
        if (!_options.UseStdio && _options.UseConsoleLogging)
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

        // Apply component-specific log levels
        foreach (var categoryLevel in _options.ComponentLogLevels)
        {
            var logEventLevel = ConvertLogLevel(categoryLevel.Value);
            loggerConfig.MinimumLevel.Override(categoryLevel.Key, logEventLevel);
        }

        // Create the Serilog logger
        _serilogLogger = loggerConfig.CreateLogger();

        // Set up Serilog.Log to use our logger (for compatibility with static logging)
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
/// Extension methods for IServiceCollection to register logging services.
/// </summary>
public static class LoggingProviderExtensions
{
    /// <summary>
    /// Adds logging services to the service collection.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure logging options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMcpLogging(
        this IServiceCollection services,
        Action<LoggingOptions>? configureOptions = null
    )
    {
        // Add options configuration
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }

        // Register the logging provider
        services.AddSingleton<ILoggingProvider, LoggingProvider>();

        // Register the logger factory
        services.AddSingleton<ILoggerFactory>(sp =>
            sp.GetRequiredService<ILoggingProvider>().CreateLoggerFactory()
        );

        return services;
    }
}
