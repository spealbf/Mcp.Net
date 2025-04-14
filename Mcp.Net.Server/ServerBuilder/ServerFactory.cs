using Mcp.Net.Server.Logging;

namespace Mcp.Net.Server.ServerBuilder;

/// <summary>
/// Factory for creating and initializing MCP servers based on configuration
/// </summary>
public class ServerFactory
{
    private readonly CommandLineOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerFactory"/> class
    /// </summary>
    /// <param name="options">Command-line options</param>
    public ServerFactory(CommandLineOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _loggerFactory = ConfigureLogging(options);
        _logger = _loggerFactory.CreateLogger<ServerFactory>();
    }

    /// <summary>
    /// Creates and runs the appropriate server type based on options
    /// </summary>
    public async Task RunServerAsync()
    {
        _logger.LogInformation(
            "MCP Server starting. UseStdio={UseStdio}, DebugMode={DebugMode}, LogPath={LogPath}",
            _options.UseStdio,
            _options.DebugMode,
            _options.LogPath
        );

        if (_options.UseStdio)
        {
            await RunStdioServerAsync();
        }
        else
        {
            RunSseServer();
        }
    }

    /// <summary>
    /// Creates and runs an SSE-based server
    /// </summary>
    private void RunSseServer()
    {
        var builder = new SseServerBuilder(_loggerFactory);
        builder.Run(_options.Args);
    }

    /// <summary>
    /// Creates and runs a stdio-based server
    /// </summary>
    private async Task RunStdioServerAsync()
    {
        var builder = new StdioServerBuilder(_loggerFactory);
        await builder.RunAsync();
    }

    /// <summary>
    /// Configures logging based on command-line options
    /// </summary>
    private static ILoggerFactory ConfigureLogging(CommandLineOptions options)
    {
        // Initialize logger with expanded configuration
        McpLoggerConfiguration.Instance.Configure(
            new McpLoggerOptions
            {
                UseStdio = options.UseStdio,
                MinimumLogLevel = options.DebugMode ? LogLevel.Debug : LogLevel.Information,
                LogFilePath = options.LogPath,
                // If using stdio, don't write logs to the console
                NoConsoleOutput = options.UseStdio,
                // Set sensible defaults for file rotation
                FileRollingInterval = Serilog.RollingInterval.Day,
                FileSizeLimitBytes = 10 * 1024 * 1024, // 10MB
                RetainedFileCountLimit = 31, // Keep a month of logs
                PrettyConsoleOutput = true,
            }
        );

        return McpLoggerConfiguration.Instance.CreateLoggerFactory();
    }
}
