using System.Reflection;
using Mcp.Net.Core.Models.Capabilities;
using Mcp.Net.Core.Transport;
using Mcp.Net.Server.Authentication;
using Mcp.Net.Server.Logging;

namespace Mcp.Net.Server.ServerBuilder;

/// <summary>
/// Builder for creating and configuring MCP servers with different transport types.
/// </summary>
public class McpServerBuilder
{
    private readonly IMcpServerBuilder _transportBuilder;
    private readonly ServerInfo _serverInfo = new();
    private LogLevel _logLevel = LogLevel.Information;
    private bool _useConsoleLogging = true;
    private string? _logFilePath = "mcp-server.log";
    private Assembly? _toolAssembly;
    internal readonly List<Assembly> _additionalToolAssemblies = new();
    private ServerOptions? _options;
    private readonly ServiceCollection _services = new();
    private IAuthentication? _authentication;
    private IApiKeyValidator? _apiKeyValidator;
    private ILoggerFactory? _loggerFactory;
    private bool _securityConfigured = false;
    private bool _noAuthExplicitlyConfigured = false;

    /// <summary>
    /// Creates a new server builder for stdio transport.
    /// </summary>
    /// <returns>A new McpServerBuilder configured for stdio transport</returns>
    public static McpServerBuilder ForStdio()
    {
        var loggerFactory = CreateDefaultLoggerFactory();
        return new McpServerBuilder(new StdioServerBuilder(loggerFactory));
    }

    /// <summary>
    /// Creates a new server builder for SSE transport.
    /// </summary>
    /// <returns>A new McpServerBuilder configured for SSE transport</returns>
    public static McpServerBuilder ForSse()
    {
        var loggerFactory = CreateDefaultLoggerFactory();
        return new McpServerBuilder(new SseServerBuilder(loggerFactory));
    }

    /// <summary>
    /// Creates a new server builder for SSE transport with the specified options.
    /// </summary>
    /// <param name="options">The options to configure the server with</param>
    /// <returns>A new McpServerBuilder configured for SSE transport with the provided options</returns>
    public static McpServerBuilder ForSse(Options.SseServerOptions options)
    {
        var loggerFactory = CreateDefaultLoggerFactory();
        return new McpServerBuilder(new SseServerBuilder(loggerFactory, options));
    }

    /// <summary>
    /// Creates a default logger factory with console logging.
    /// </summary>
    private static ILoggerFactory CreateDefaultLoggerFactory()
    {
        return LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="McpServerBuilder"/> class.
    /// </summary>
    /// <param name="transportBuilder">The transport-specific builder to use</param>
    private McpServerBuilder(IMcpServerBuilder transportBuilder)
    {
        _transportBuilder =
            transportBuilder ?? throw new ArgumentNullException(nameof(transportBuilder));
    }

    /// <summary>
    /// Gets the transport builder used by this server builder.
    /// </summary>
    internal IMcpServerBuilder TransportBuilder => _transportBuilder;

    /// <summary>
    /// Gets the authentication provider configured for this server.
    /// </summary>
    public IAuthentication? Authentication => _authentication;

    /// <summary>
    /// Gets the API key validator configured for this server.
    /// </summary>
    public IApiKeyValidator? ApiKeyValidator => _apiKeyValidator;
    
    /// <summary>
    /// Gets the log level configured for this server.
    /// </summary>
    public LogLevel LogLevel => _logLevel;
    
    /// <summary>
    /// Gets whether console logging is enabled for this server.
    /// </summary>
    public bool UseConsoleLogging => _useConsoleLogging;
    
    /// <summary>
    /// Gets the log file path configured for this server.
    /// </summary>
    public string? LogFilePath => _logFilePath;

    /// <summary>
    /// Configures the server with a specific name.
    /// </summary>
    /// <param name="name">The name of the server</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder WithName(string name)
    {
        _serverInfo.Name = name;
        return this;
    }

    /// <summary>
    /// Configures the server with a specific version.
    /// </summary>
    /// <param name="version">The version of the server</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder WithVersion(string version)
    {
        _serverInfo.Version = version;
        return this;
    }

    /// <summary>
    /// Configures the server with specific instructions.
    /// </summary>
    /// <param name="instructions">The instructions for the server</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder WithInstructions(string instructions)
    {
        if (_options == null)
            _options = new ServerOptions();

        _options.Instructions = instructions;
        return this;
    }

    /// <summary>
    /// Configures the server with a specific port (SSE transport only).
    /// </summary>
    /// <param name="port">The port to use</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder WithPort(int port)
    {
        if (_transportBuilder is SseServerBuilder sseBuilder)
        {
            sseBuilder.WithPort(port);
        }
        else
        {
            throw new InvalidOperationException("WithPort is only valid for SSE transport");
        }

        return this;
    }

    /// <summary>
    /// Configures the server with a specific hostname (SSE transport only).
    /// </summary>
    /// <param name="hostname">The hostname to bind to</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder WithHostname(string hostname)
    {
        if (_transportBuilder is SseServerBuilder sseBuilder)
        {
            sseBuilder.WithHostname(hostname);
        }
        else
        {
            throw new InvalidOperationException("WithHostname is only valid for SSE transport");
        }

        return this;
    }

    /// <summary>
    /// Configures the server to use API key authentication.
    /// </summary>
    /// <param name="apiKey">The API key to authenticate with</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder WithApiKey(string apiKey)
    {
        _securityConfigured = true;
        var loggerFactory = _loggerFactory ?? CreateLoggerFactory();
        var validator = AuthenticationConfigurator.CreateApiKeyValidator(loggerFactory, apiKey);
        return WithApiKeyValidator(validator);
    }

    /// <summary>
    /// Configures the server to use API key authentication with multiple valid keys.
    /// </summary>
    /// <param name="apiKeys">Collection of valid API keys</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder WithApiKeys(IEnumerable<string> apiKeys)
    {
        _securityConfigured = true;
        var loggerFactory = _loggerFactory ?? CreateLoggerFactory();
        var validator = AuthenticationConfigurator.CreateApiKeyValidator(loggerFactory, apiKeys);
        return WithApiKeyValidator(validator);
    }

    /// <summary>
    /// Configures the server to use API key authentication with a custom validator.
    /// </summary>
    /// <param name="validator">The custom validator to use</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder WithApiKeyValidator(IApiKeyValidator validator)
    {
        _securityConfigured = true;
        _apiKeyValidator = validator;

        // Configure the SSE builder with the validator if applicable
        if (_transportBuilder is SseServerBuilder sseBuilder)
        {
            sseBuilder.WithApiKeyValidator(validator);
        }

        return this;
    }

    /// <summary>
    /// Configures the server to use API key authentication with options.
    /// </summary>
    /// <param name="configure">Action to configure the API key options</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder WithApiKeyOptions(Action<ApiKeyAuthOptions> configure)
    {
        _securityConfigured = true;

        // Create default options
        var options = new ApiKeyAuthOptions();

        // Apply configuration
        configure(options);

        // Create validator with a default API key if needed
        var loggerFactory = _loggerFactory ?? CreateLoggerFactory();
        var logger = loggerFactory.CreateLogger<InMemoryApiKeyValidator>();
        var validator = new InMemoryApiKeyValidator(logger);

        // Add a default API key if none was configured
        if (string.IsNullOrEmpty(options.DefaultApiKey) == false)
        {
            validator.AddApiKey(options.DefaultApiKey, "default-user");
        }

        _apiKeyValidator = validator;

        // Configure the SSE builder with the validator if applicable
        if (_transportBuilder is SseServerBuilder sseBuilder)
        {
            sseBuilder.WithApiKeyValidator(validator);
        }

        return this;
    }

    /// <summary>
    /// Configures the server to use a custom authentication mechanism.
    /// </summary>
    /// <param name="authentication">The custom authentication mechanism</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder WithAuthentication(IAuthentication authentication)
    {
        _securityConfigured = true;
        _authentication = authentication;

        // Configure the SSE builder with the authentication if applicable
        if (_transportBuilder is SseServerBuilder sseBuilder)
        {
            sseBuilder.WithAuthentication(authentication);
        }

        return this;
    }

    /// <summary>
    /// Configures the server to not use any authentication.
    /// </summary>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder WithNoAuth()
    {
        _securityConfigured = true;
        _noAuthExplicitlyConfigured = true;
        return this;
    }

    /// <summary>
    /// Configures the server with a specific log level.
    /// </summary>
    /// <param name="level">The log level to use</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder WithLogLevel(LogLevel level)
    {
        _logLevel = level;
        return this;
    }

    /// <summary>
    /// Configures the server to use console logging.
    /// </summary>
    /// <param name="useConsole">Whether to log to console</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder WithConsoleLogging(bool useConsole = true)
    {
        _useConsoleLogging = useConsole;
        return this;
    }

    /// <summary>
    /// Configures the server with a specific log file path.
    /// </summary>
    /// <param name="filePath">The path to log to, or null to disable file logging</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder WithLogFile(string? filePath)
    {
        _logFilePath = filePath;
        return this;
    }

    /// <summary>
    /// Configures common log levels for specific components.
    /// </summary>
    /// <param name="toolsLevel">Log level for tools</param>
    /// <param name="transportLevel">Log level for transport</param>
    /// <param name="jsonRpcLevel">Log level for JSON-RPC</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder ConfigureCommonLogLevels(
        LogLevel toolsLevel = LogLevel.Information,
        LogLevel transportLevel = LogLevel.Information,
        LogLevel jsonRpcLevel = LogLevel.Information
    )
    {
        // This would typically be configured in the logger factory
        // For now, we'll just set the overall log level to the most verbose level
        _logLevel = new[] { toolsLevel, transportLevel, jsonRpcLevel }.Min();
        return this;
    }

    /// <summary>
    /// Configures the server to scan an assembly for tools.
    /// </summary>
    /// <param name="assembly">The assembly to scan</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder ScanToolsFromAssembly(Assembly assembly)
    {
        _toolAssembly = assembly;
        return this;
    }

    /// <summary>
    /// Configures the server to scan an additional assembly for tools.
    /// </summary>
    /// <param name="assembly">The additional assembly to scan</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder ScanAdditionalToolsFromAssembly(Assembly assembly)
    {
        _additionalToolAssemblies.Add(assembly);
        return this;
    }

    /// <summary>
    /// Configures the server to scan an additional assembly for tools (alias for ScanAdditionalToolsFromAssembly).
    /// </summary>
    /// <param name="assembly">The additional assembly to scan</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder WithAdditionalAssembly(Assembly assembly)
    {
        return ScanAdditionalToolsFromAssembly(assembly);
    }

    /// <summary>
    /// Registers additional services for dependency injection.
    /// </summary>
    /// <param name="configureServices">The action to configure services</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder ConfigureServices(Action<IServiceCollection> configureServices)
    {
        configureServices(_services);
        return this;
    }

    /// <summary>
    /// Registers a specific logger factory.
    /// </summary>
    /// <param name="loggerFactory">The logger factory to use</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder WithLoggerFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        return this;
    }

    /// <summary>
    /// Builds and starts the server asynchronously.
    /// </summary>
    /// <returns>A task representing the running server</returns>
    public async Task<McpServer> StartAsync()
    {
        var server = Build();
        await _transportBuilder.StartAsync(server);
        return server;
    }

    /// <summary>
    /// Builds the server without starting it.
    /// </summary>
    /// <returns>The built server instance</returns>
    public McpServer Build()
    {
        // Set up logging
        var loggerFactory = _loggerFactory ?? CreateLoggerFactory();

        // Set up server info/options
        var serverOptions =
            _options
            ?? new ServerOptions
            {
                Capabilities = new ServerCapabilities
                {
                    Tools = new { },
                    Resources = new { },
                    Prompts = new { },
                },
            };

        // Use security configuration if needed
        ConfigureSecurity();

        // Create server
        var server = new McpServer(_serverInfo, serverOptions, loggerFactory);

        // NOTE: We don't register tools here anymore.
        // Tool registration now happens exclusively in McpServerServiceCollectionExtensions.RegisterServerAndTools
        // via the DI container to ensure all tool registrations happen on the same server instance

        return server;
    }

    /// <summary>
    /// Configures security based on the builder settings.
    /// </summary>
    private void ConfigureSecurity()
    {
        // This method ensures the _securityConfigured and _noAuthExplicitlyConfigured fields are used
        if (_securityConfigured)
        {
            // Authentication explicitly configured
            var logger = (
                _loggerFactory ?? CreateDefaultLoggerFactory()
            ).CreateLogger<McpServerBuilder>();

            if (_noAuthExplicitlyConfigured)
            {
                logger.LogInformation("Authentication explicitly disabled");
            }
            else if (_authentication != null)
            {
                logger.LogInformation("Using custom authentication provider");
            }
            else if (_apiKeyValidator != null)
            {
                logger.LogInformation("Using API key authentication");
            }
        }
    }

    /// <summary>
    /// Creates a new logger factory with configured settings.
    /// </summary>
    private ILoggerFactory CreateLoggerFactory()
    {
        // If a logger factory was provided, use it
        if (_loggerFactory != null)
        {
            return _loggerFactory;
        }

        // Create options based on builder settings
        var options = new Options.LoggingOptions
        {
            MinimumLogLevel = _logLevel,
            UseConsoleLogging = _useConsoleLogging,
            LogFilePath = _logFilePath ?? "logs/mcp-server.log",
            UseStdio = !_useConsoleLogging,
        };

        // Create a logging provider with our options
        var loggingProvider = new Options.LoggingProvider(options);

        // Create and return the logger factory
        return loggingProvider.CreateLoggerFactory();
    }
}
