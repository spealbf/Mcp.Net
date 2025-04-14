using Microsoft.Extensions.Logging;
using Serilog;

namespace Mcp.Net.Server.Options;

/// <summary>
/// Represents options for configuring logging in an MCP server.
/// </summary>
public class LoggingOptions
{
    /// <summary>
    /// Gets or sets the minimum log level.
    /// </summary>
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// Gets or sets whether console logging is enabled.
    /// </summary>
    public bool UseConsoleLogging { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to use standard output instead of the console logger.
    /// </summary>
    public bool UseStdio { get; set; } = false;

    /// <summary>
    /// Gets or sets the path to the log file.
    /// </summary>
    public string? LogFilePath { get; set; } = "mcp-server.log";

    /// <summary>
    /// Gets or sets whether file logging is enabled.
    /// </summary>
    public bool UseFileLogging => !string.IsNullOrEmpty(LogFilePath);

    /// <summary>
    /// Gets or sets whether to format console output for better readability.
    /// </summary>
    public bool PrettyConsoleOutput { get; set; } = true;

    /// <summary>
    /// Gets or sets the log file rolling interval.
    /// </summary>
    public RollingInterval FileRollingInterval { get; set; } = RollingInterval.Day;

    /// <summary>
    /// Gets or sets the maximum log file size in bytes.
    /// </summary>
    public long FileSizeLimitBytes { get; set; } = 10 * 1024 * 1024; // 10MB

    /// <summary>
    /// Gets or sets the maximum number of log files to retain.
    /// </summary>
    public int RetainedFileCountLimit { get; set; } = 31;

    /// <summary>
    /// Gets or sets component-specific log levels.
    /// </summary>
    public Dictionary<string, LogLevel> ComponentLogLevels { get; set; } = new();

    /// <summary>
    /// Configures the options with a specific log level.
    /// </summary>
    /// <param name="level">The log level</param>
    /// <returns>The options instance for chaining</returns>
    public LoggingOptions WithLogLevel(LogLevel level)
    {
        MinimumLogLevel = level;
        return this;
    }

    /// <summary>
    /// Configures the options to use console logging.
    /// </summary>
    /// <param name="useConsole">Whether to log to console</param>
    /// <returns>The options instance for chaining</returns>
    public LoggingOptions WithConsoleLogging(bool useConsole = true)
    {
        UseConsoleLogging = useConsole;
        return this;
    }

    /// <summary>
    /// Configures the options with a specific log file path.
    /// </summary>
    /// <param name="filePath">The path to log to, or null to disable file logging</param>
    /// <returns>The options instance for chaining</returns>
    public LoggingOptions WithLogFile(string? filePath)
    {
        LogFilePath = filePath;
        return this;
    }

    /// <summary>
    /// Configures a log level for a specific component.
    /// </summary>
    /// <param name="componentName">The component name</param>
    /// <param name="level">The log level</param>
    /// <returns>The options instance for chaining</returns>
    public LoggingOptions WithComponentLogLevel(string componentName, LogLevel level)
    {
        ComponentLogLevels[componentName] = level;
        return this;
    }

    /// <summary>
    /// Configures the options for pretty console output.
    /// </summary>
    /// <param name="usePrettyOutput">Whether to use prettier console formatting</param>
    /// <returns>The options instance for chaining</returns>
    public LoggingOptions WithPrettyConsoleOutput(bool usePrettyOutput = true)
    {
        PrettyConsoleOutput = usePrettyOutput;
        return this;
    }

    /// <summary>
    /// Configures the options for file rolling.
    /// </summary>
    /// <param name="interval">The rolling interval</param>
    /// <returns>The options instance for chaining</returns>
    public LoggingOptions WithFileRollingInterval(RollingInterval interval)
    {
        FileRollingInterval = interval;
        return this;
    }

    /// <summary>
    /// Configures the options for file size limit.
    /// </summary>
    /// <param name="sizeInMb">The maximum size in megabytes</param>
    /// <returns>The options instance for chaining</returns>
    public LoggingOptions WithFileSizeLimit(int sizeInMb)
    {
        FileSizeLimitBytes = sizeInMb * 1024 * 1024;
        return this;
    }

    /// <summary>
    /// Configures the options for retained file count.
    /// </summary>
    /// <param name="count">The number of files to retain</param>
    /// <returns>The options instance for chaining</returns>
    public LoggingOptions WithRetainedFileCount(int count)
    {
        RetainedFileCountLimit = count;
        return this;
    }

    /// <summary>
    /// Converts to McpLoggingOptions for backward compatibility.
    /// </summary>
    /// <returns>Equivalent McpLoggingOptions instance</returns>
    public Logging.McpLoggingOptions ToMcpLoggingOptions()
    {
        return new Logging.McpLoggingOptions
        {
            MinimumLogLevel = MinimumLogLevel,
            NoConsoleOutput = !UseConsoleLogging,
            LogFilePath = LogFilePath ?? "logs/mcp-server.log",
            UseStdio = UseStdio,
            PrettyConsoleOutput = PrettyConsoleOutput,
            FileRollingInterval = FileRollingInterval,
            FileSizeLimitBytes = FileSizeLimitBytes,
            RetainedFileCountLimit = RetainedFileCountLimit,
            CategoryLogLevels = new Dictionary<string, LogLevel>(ComponentLogLevels),
        };
    }

    /// <summary>
    /// Creates a new LoggingOptions from the specified McpLoggingOptions.
    /// </summary>
    /// <param name="options">The McpLoggingOptions to convert from</param>
    /// <returns>A new LoggingOptions instance</returns>
    public static LoggingOptions FromMcpLoggingOptions(Logging.McpLoggingOptions options)
    {
        return new LoggingOptions
        {
            MinimumLogLevel = options.MinimumLogLevel,
            UseConsoleLogging = !options.NoConsoleOutput,
            LogFilePath = options.LogFilePath,
            UseStdio = options.UseStdio,
            PrettyConsoleOutput = options.PrettyConsoleOutput,
            FileRollingInterval = options.FileRollingInterval,
            FileSizeLimitBytes = options.FileSizeLimitBytes,
            RetainedFileCountLimit = options.RetainedFileCountLimit,
            ComponentLogLevels = new Dictionary<string, LogLevel>(options.CategoryLogLevels),
        };
    }

    /// <summary>
    /// Creates a new LoggingOptions from the specified McpLoggerOptions.
    /// </summary>
    /// <param name="options">The McpLoggerOptions to convert from</param>
    /// <returns>A new LoggingOptions instance</returns>
    public static LoggingOptions FromMcpLoggerOptions(Logging.McpLoggerOptions options)
    {
        return new LoggingOptions
        {
            MinimumLogLevel = options.MinimumLogLevel,
            UseConsoleLogging = !options.NoConsoleOutput,
            LogFilePath = options.LogFilePath,
            UseStdio = options.UseStdio,
            PrettyConsoleOutput = options.PrettyConsoleOutput,
            FileRollingInterval = options.FileRollingInterval,
            FileSizeLimitBytes = options.FileSizeLimitBytes,
            RetainedFileCountLimit = options.RetainedFileCountLimit,
            ComponentLogLevels = new Dictionary<string, LogLevel>(options.CategoryLogLevels),
        };
    }
}
