using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Mcp.Net.Server.Logging;

namespace Mcp.Net.Server.ServerBuilder;

/// <summary>
/// Factory for creating and initializing MCP servers based on configuration
/// </summary>
public class ServerFactory
{
    private readonly CommandLineOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<ServerFactory> _logger;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerFactory"/> class
    /// </summary>
    /// <param name="options">Command-line options</param>
    /// <param name="loggerFactory">The logger factory to use</param>
    /// <param name="configuration">The configuration to use</param>
    public ServerFactory(CommandLineOptions options, ILoggerFactory loggerFactory, IConfiguration configuration)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
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
}