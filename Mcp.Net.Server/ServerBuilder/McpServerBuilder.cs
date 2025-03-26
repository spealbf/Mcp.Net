using System.Reflection;
using Mcp.Net.Core.Interfaces;
using Mcp.Net.Core.Models.Capabilities;
using Mcp.Net.Server.Authentication;
using Mcp.Net.Server.Extensions;
using Mcp.Net.Server.Logging;
using Mcp.Net.Server.Transport.Sse;
using Serilog;

namespace Mcp.Net.Server.ServerBuilder;

public class McpServerBuilder
{
    private readonly ServerInfo _serverInfo = new();
    private LogLevel _logLevel = LogLevel.Information;
    private bool _useConsoleLogging = true;
    private string? _logFilePath = "mcp-server.log";
    private Func<ITransport>? _transportFactory;
    private Assembly? _toolAssembly;
    private readonly List<Assembly> _additionalToolAssemblies = new();
    private ServerOptions? _options;
    private readonly ServiceCollection _services = new();
    private bool _useSse = false;
    private string _sseBaseUrl = "http://localhost:5000";
    private readonly McpServerConfiguration _serverConfiguration = new();
    private IAuthentication? _authentication;
    private IApiKeyValidator? _apiKeyValidator;
    private ILoggerFactory? _loggerFactory;
    private bool _securityConfigured = false;
    private bool _noAuthExplicitlyConfigured = false;

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

    /// <summary>
    /// Configure the server to use SSE transport
    /// </summary>
    /// <param name="baseUrl">Optional base URL for the SSE transport</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder UseSseTransport(string? baseUrl = null)
    {
        _useSse = true;
        if (baseUrl != null)
        {
            _sseBaseUrl = baseUrl;
        }
        return this;
    }

    /// <summary>
    /// Configure the server port
    /// </summary>
    /// <param name="port">The port to use for the SSE transport</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder UsePort(int port)
    {
        _serverConfiguration.Port = port;
        _sseBaseUrl = _serverConfiguration.BaseUrl;
        return this;
    }

    /// <summary>
    /// Configure the server hostname
    /// </summary>
    /// <param name="hostname">The hostname to bind to</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder UseHostname(string hostname)
    {
        _serverConfiguration.Hostname = hostname;
        _sseBaseUrl = _serverConfiguration.BaseUrl;
        return this;
    }

    /// <summary>
    /// Configure the server using predefined configuration
    /// </summary>
    /// <param name="configuration">The server configuration</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder UseConfiguration(McpServerConfiguration configuration)
    {
        _serverConfiguration.Port = configuration.Port;
        _serverConfiguration.Hostname = configuration.Hostname;
        _sseBaseUrl = _serverConfiguration.BaseUrl;
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

    /// <summary>
    /// Sets the primary tool assembly, replacing the default entry assembly
    /// </summary>
    /// <param name="assembly">The assembly to load tools from</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder WithAssembly(Assembly assembly)
    {
        _toolAssembly = assembly;
        return this;
    }

    /// <summary>
    /// Adds an additional assembly to load tools from, alongside the primary assembly
    /// </summary>
    /// <param name="assembly">The additional assembly to load tools from</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder WithAdditionalAssembly(Assembly assembly)
    {
        _additionalToolAssemblies.Add(assembly);
        return this;
    }

    public McpServerBuilder AddServices(Action<IServiceCollection> configureServices)
    {
        configureServices(_services);
        return this;
    }

    public McpServer Build()
    {
        // Validate security configuration when using SSE transport
        if (_useSse && !_securityConfigured)
        {
            var logger =
                _loggerFactory?.CreateLogger<McpServerBuilder>()
                ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<McpServerBuilder>.Instance;

            logger.LogWarning(
                "Security not configured for SSE transport. This is not recommended for production use. "
                    + "Call UseApiKeyAuthentication() or explicitly disable security with UseNoAuthentication()."
            );
        }
        else if (_useSse && _securityConfigured && _noAuthExplicitlyConfigured)
        {
            var logger =
                _loggerFactory?.CreateLogger<McpServerBuilder>()
                ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<McpServerBuilder>.Instance;

            logger.LogWarning(
                "Server is explicitly configured with NO AUTHENTICATION for SSE transport. "
                    + "This configuration is not recommended for production use."
            );
        }

        ConfigureLogging();

        var server = new McpServer(_serverInfo, _options);

        // Build service provider for registering tools
        var serviceProvider = _services.BuildServiceProvider();

        // Register tools from primary assembly (entry assembly or specified assembly)
        var primaryAssembly =
            _toolAssembly ?? Assembly.GetEntryAssembly() ?? Assembly.GetCallingAssembly();
        server.RegisterToolsFromAssembly(primaryAssembly, serviceProvider);

        // Register tools from any additional assemblies
        foreach (var assembly in _additionalToolAssemblies)
        {
            server.RegisterToolsFromAssembly(assembly, serviceProvider);
        }

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

        // Create the logger factory and store it for later use
        _loggerFactory = McpLoggerConfiguration.Instance.CreateLoggerFactory();

        // Log the configuration to help with debugging
        var initialLogger = _loggerFactory.CreateLogger("Builder");
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

    public McpServerConfiguration ServerConfiguration => _serverConfiguration;

    public int Port
    {
        get => _serverConfiguration.Port;
        set
        {
            _serverConfiguration.Port = value;
            _sseBaseUrl = _serverConfiguration.BaseUrl;
        }
    }

    public string Hostname
    {
        get => _serverConfiguration.Hostname;
        set
        {
            _serverConfiguration.Hostname = value;
            _sseBaseUrl = _serverConfiguration.BaseUrl;
        }
    }

    /// <summary>
    /// Configures API key authentication for the server
    /// </summary>
    /// <param name="configureOptions">Action to configure API key authentication options</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder UseApiKeyAuthentication(
        Action<ApiKeyAuthOptions>? configureOptions = null
    )
    {
        // Create options
        var options = new ApiKeyAuthOptions();
        configureOptions?.Invoke(options);

        // Ensure we have a logger factory
        if (_loggerFactory == null)
        {
            // Configure logging if it hasn't been done already
            ConfigureLogging();
        }

        // Register API key validator
        var validator = new InMemoryApiKeyValidator(
            _loggerFactory!.CreateLogger<InMemoryApiKeyValidator>()
        );
        _apiKeyValidator = validator;

        // Register services
        _services.AddSingleton<IApiKeyValidator>(validator);

        // Create authentication handler
        var authHandler = new ApiKeyAuthenticationHandler(
            options,
            validator,
            _loggerFactory!.CreateLogger<ApiKeyAuthenticationHandler>()
        );

        // Register the authentication handler in DI
        _services.AddSingleton<IAuthentication>(authHandler);

        // Store locally
        _authentication = authHandler;

        // Mark security as configured
        _securityConfigured = true;

        return this;
    }

    /// <summary>
    /// Explicitly configures the server to use no authentication
    /// </summary>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder UseNoAuthentication()
    {
        // Ensure we have a logger factory
        if (_loggerFactory == null)
        {
            // Configure logging if it hasn't been done already
            ConfigureLogging();
        }

        _loggerFactory!
            .CreateLogger<McpServerBuilder>()
            .LogWarning(
                "Server configured with NO AUTHENTICATION - not recommended for production"
            );

        // Mark security as explicitly configured with no authentication
        _securityConfigured = true;
        _noAuthExplicitlyConfigured = true;

        return this;
    }

    /// <summary>
    /// Gets the API key validator, if one has been configured
    /// </summary>
    public IApiKeyValidator? ApiKeyValidator => _apiKeyValidator;

    /// <summary>
    /// Gets the authentication handler, if one has been configured
    /// </summary>
    public IAuthentication? Authentication => _authentication;
}
