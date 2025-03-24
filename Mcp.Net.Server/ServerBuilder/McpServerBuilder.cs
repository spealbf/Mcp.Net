using System;
using System.Reflection;
using System.Threading.Tasks;
using Mcp.Net.Core;
using Mcp.Net.Core.Interfaces;
using Mcp.Net.Core.JsonRpc;
using Mcp.Net.Core.Models.Capabilities;
using Mcp.Net.Server.Extensions;
using Mcp.Net.Server.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.Server.ServerBuilder;

/// <summary>
/// Interface for creating SSE transports
/// </summary>
internal interface ISseTransportFactory
{
    /// <summary>
    /// Creates a new SSE transport for the given response
    /// </summary>
    /// <param name="response">The HTTP response to use</param>
    /// <returns>A new SSE transport</returns>
    SseTransport CreateTransport(HttpResponse response);
}

/// <summary>
/// Factory for creating and registering SSE transports
/// </summary>
internal class SseTransportFactory : ISseTransportFactory
{
    private readonly SseConnectionManager _connectionManager;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SseTransportFactory"/> class
    /// </summary>
    /// <param name="connectionManager">Connection manager for SSE transports</param>
    /// <param name="loggerFactory">Logger factory</param>
    public SseTransportFactory(SseConnectionManager connectionManager, ILoggerFactory loggerFactory)
    {
        _connectionManager = connectionManager;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc />
    public SseTransport CreateTransport(HttpResponse response)
    {
        // Create HTTP response writer
        var responseWriter = new HttpResponseWriter(
            response,
            _loggerFactory.CreateLogger<HttpResponseWriter>()
        );

        // Create SSE transport with the response writer
        var transport = new SseTransport(
            responseWriter,
            _loggerFactory.CreateLogger<SseTransport>()
        );

        // Register with connection manager
        _connectionManager.RegisterTransport(transport);

        return transport;
    }
}

public class McpServerBuilder
{
    private readonly ServerInfo _serverInfo = new();
    private LogLevel _logLevel = LogLevel.Information;
    private bool _useConsoleLogging = true;
    private string? _logFilePath = "mcp-server.log";
    private Func<ITransport>? _transportFactory;
    private Assembly? _toolAssembly;
    private ServerOptions? _options;
    private readonly ServiceCollection _services = new();
    private bool _useSse = false;
    private string _sseBaseUrl = "http://localhost:5050";

    public McpServerBuilder WithName(string name)
    {
        _serverInfo.Name = name;
        return this;
    }

    public McpServerBuilder WithVersion(string version)
    {
        _serverInfo.Version = version;
        return this;
    }

    public McpServerBuilder WithInstructions(string instructions)
    {
        if (_options == null)
            _options = new ServerOptions();

        _options.Instructions = instructions;
        return this;
    }

    public McpServerBuilder UseStdioTransport()
    {
        _transportFactory = () =>
        {
            // Create components for transport - use default constructor with NullLogger
            // This is critical to avoid any console logging in stdio mode!
            var input = Console.OpenStandardInput();
            var output = Console.OpenStandardOutput();

            // Create and return transport with NullLogger
            return new StdioTransport(input, output);
        };
        return this;
    }

    public McpServerBuilder UseSseTransport(string baseUrl = "http://localhost:5000")
    {
        _useSse = true;
        _sseBaseUrl = baseUrl;
        return this;
    }

    public McpServerBuilder UseLogLevel(LogLevel level)
    {
        _logLevel = level;
        return this;
    }

    public McpServerBuilder UseConsoleLogging(bool enabled = true)
    {
        _useConsoleLogging = enabled;
        return this;
    }

    public McpServerBuilder UseFileLogging(string path)
    {
        _logFilePath = path;
        return this;
    }

    public McpServerBuilder WithAssembly(Assembly assembly)
    {
        _toolAssembly = assembly;
        return this;
    }

    public McpServerBuilder AddServices(Action<IServiceCollection> configureServices)
    {
        configureServices(_services);
        return this;
    }

    public McpServer Build()
    {
        ConfigureLogging();

        var server = new McpServer(_serverInfo, _options);

        // Build service provider for registering tools
        var serviceProvider = _services.BuildServiceProvider();

        var toolAssembly =
            _toolAssembly ?? Assembly.GetEntryAssembly() ?? Assembly.GetCallingAssembly();
        server.RegisterToolsFromAssembly(toolAssembly, serviceProvider);

        return server;
    }

    public async Task<McpServer> StartAsync()
    {
        var server = Build();

        if (_transportFactory == null)
        {
            // Use the same logic as UseStdioTransport
            UseStdioTransport();
        }

        // Ensure the transport factory is not null after the check
        var transport = _transportFactory!();
        await server.ConnectAsync(transport);

        return server;
    }

    private void ConfigureLogging()
    {
        // Initialize the logger with appropriate options
        Logger.Initialize(
            new LoggerOptions
            {
                UseStdio = IsStdioTransport(),
                DebugMode = _logLevel <= LogLevel.Debug,
                LogFilePath = _logFilePath ?? "mcp-server.log",
                // Always disable console output in stdio mode for safety
                NoConsoleOutput = IsStdioTransport(),
            }
        );

        // Log the configuration to help with debugging
        Logger.Information(
            "Logger initialized: stdio={UseStdio}, debug={Debug}, logfile={LogFile}",
            IsStdioTransport(),
            _logLevel <= LogLevel.Debug,
            _logFilePath ?? "mcp-server.log"
        );
    }

    private bool IsStdioTransport()
    {
        if (_useSse)
            return false;

        if (_transportFactory == null)
            return true;

        var transport = _transportFactory();
        return transport is StdioTransport;
    }

    public bool IsUsingSse => _useSse;

    public string SseBaseUrl => _sseBaseUrl;
}
