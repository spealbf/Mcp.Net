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
using Serilog;

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

    /// <summary>
    /// Configures log file rotation and retention
    /// </summary>
    /// <param name="rollingInterval">The interval at which to roll log files</param>
    /// <param name="maxSizeMb">The maximum size of a log file in megabytes</param>
    /// <param name="retainedFileCount">The number of log files to retain</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder ConfigureFileRotation(
        RollingInterval rollingInterval = RollingInterval.Day,
        int maxSizeMb = 10,
        int retainedFileCount = 31
    )
    {
        var options = McpLoggerConfiguration.Instance.Options;
        options.FileRollingInterval = rollingInterval;
        options.FileSizeLimitBytes = maxSizeMb * 1024 * 1024;
        options.RetainedFileCountLimit = retainedFileCount;

        return this;
    }

    /// <summary>
    /// Sets the log level for a specific category
    /// </summary>
    /// <param name="category">The category to set the log level for</param>
    /// <param name="level">The log level to set</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder SetCategoryLogLevel(string category, LogLevel level)
    {
        var options = McpLoggerConfiguration.Instance.Options;
        options.CategoryLogLevels[category] = level;

        return this;
    }

    /// <summary>
    /// Sets log levels for multiple categories at once
    /// </summary>
    /// <param name="categoryLevels">Dictionary mapping categories to log levels</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder SetCategoryLogLevels(Dictionary<string, LogLevel> categoryLevels)
    {
        var options = McpLoggerConfiguration.Instance.Options;

        foreach (var kvp in categoryLevels)
        {
            options.CategoryLogLevels[kvp.Key] = kvp.Value;
        }

        return this;
    }

    /// <summary>
    /// Configures common category log levels for the MCP server
    /// </summary>
    /// <param name="toolsLevel">Log level for tool-related categories</param>
    /// <param name="transportLevel">Log level for transport-related categories</param>
    /// <param name="jsonRpcLevel">Log level for JSON-RPC related categories</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder ConfigureCommonLogLevels(
        LogLevel toolsLevel = LogLevel.Information,
        LogLevel transportLevel = LogLevel.Information,
        LogLevel jsonRpcLevel = LogLevel.Warning
    )
    {
        var categoryLevels = new Dictionary<string, LogLevel>
        {
            // Transport categories
            ["Mcp.Net.Server.StdioTransport"] = transportLevel,
            ["Mcp.Net.Server.SseTransport"] = transportLevel,
            ["Mcp.Net.Server.HttpResponseWriter"] = transportLevel,
            ["Mcp.Net.Server.SseConnectionManager"] = transportLevel,

            // JSON-RPC categories
            ["Mcp.Net.Core.JsonRpc"] = jsonRpcLevel,
            ["Mcp.Net.Server.McpServer"] = jsonRpcLevel,

            // Tool-related categories
            ["Mcp.Net.Server.Extensions.McpServerExtensions"] = toolsLevel,
        };

        return SetCategoryLogLevels(categoryLevels);
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
        // Create options using the modern configuration pattern
        var options = new McpLoggerOptions
        {
            UseStdio = IsStdioTransport(),
            MinimumLogLevel = _logLevel,
            LogFilePath = _logFilePath ?? "mcp-server.log",
            // Always disable console output in stdio mode for safety
            NoConsoleOutput = IsStdioTransport(),
            // Set sensible defaults for file rotation
            FileRollingInterval = RollingInterval.Day,
            FileSizeLimitBytes = 10 * 1024 * 1024, // 10MB
            RetainedFileCountLimit = 31, // Keep a month of logs
        };

        // Configure the logger
        McpLoggerConfiguration.Instance.Configure(options);

        // Log the configuration to help with debugging
        var initialLogger = McpLoggerConfiguration
            .Instance.CreateLoggerFactory()
            .CreateLogger("Builder");
        initialLogger.LogInformation(
            "Logger initialized: stdio={UseStdio}, logLevel={LogLevel}, logfile={LogFile}",
            IsStdioTransport(),
            _logLevel.ToString(),
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
