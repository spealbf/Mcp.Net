using System.Reflection;
using Mcp.Net.Core.Models.Capabilities;
using Mcp.Net.Server.Extensions;
using Mcp.Net.Server.Transport.Stdio;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.Server.ServerBuilder;

/// <summary>
/// Builder for creating and configuring a stdio-based MCP server
/// </summary>
public class StdioServerBuilder
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StdioServerBuilder"/> class
    /// </summary>
    /// <param name="loggerFactory">Logger factory for creating loggers</param>
    public StdioServerBuilder(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = _loggerFactory.CreateLogger<StdioServerBuilder>();
    }

    /// <summary>
    /// Configures and runs a stdio-based MCP server
    /// </summary>
    public async Task RunAsync()
    {
        _logger.LogInformation("Starting MCP server with stdio transport");

        // Create server with default capabilities
        var mcpServer = CreateMcpServer();

        var services = new ServiceCollection()
            .AddSingleton(mcpServer)
            .AddSingleton(_loggerFactory)
            .BuildServiceProvider();

        mcpServer.RegisterToolsFromAssembly(Assembly.GetExecutingAssembly(), services);
        _logger.LogInformation("Registered tools from assembly");

        var transport = CreateStdioTransport();

        await mcpServer.ConnectAsync(transport);
        _logger.LogInformation("Server connected to stdio transport");

        var tcs = new TaskCompletionSource<bool>();
        transport.OnClose += () => tcs.TrySetResult(true);

        _logger.LogInformation("MCP server running with stdio transport");
        await tcs.Task;
    }

    /// <summary>
    /// Creates an MCP server with default settings
    /// </summary>
    private McpServer CreateMcpServer()
    {
        return new McpServer(
            new ServerInfo { Name = "example-server", Version = "1.0.0" },
            new ServerOptions
            {
                Capabilities = new ServerCapabilities { Tools = new { } },
                Instructions = "This server provides dynamic tools",
            },
            _loggerFactory
        );
    }

    /// <summary>
    /// Creates and configures a stdio transport
    /// </summary>
    private StdioTransport CreateStdioTransport()
    {
        var transport = new StdioTransport(
            Console.OpenStandardInput(),
            Console.OpenStandardOutput(),
            _loggerFactory.CreateLogger<StdioTransport>()
        );

        // Configure transport event handlers
        transport.OnRequest += request =>
        {
            _logger.LogDebug(
                "JSON-RPC Request: Method={Method}, Id={Id}",
                request.Method,
                request.Id ?? "null"
            );
        };

        transport.OnError += ex =>
        {
            _logger.LogError(ex, "Stdio transport error");
        };

        transport.OnClose += () =>
        {
            _logger.LogInformation("Stdio transport closed");
        };

        return transport;
    }
}
