using System.Reflection;
using Mcp.Net.Core.Models.Capabilities;
using Mcp.Net.Core.Transport;
using Mcp.Net.Server.Authentication;
using Mcp.Net.Server.Extensions;
using Mcp.Net.Server.Logging;
using Mcp.Net.Server.Transport.Stdio;

namespace Mcp.Net.Server.ServerBuilder;

public class McpServerBuilder
{
    private readonly ServerInfo _serverInfo = new();
    private LogLevel _logLevel = LogLevel.Information;
    private bool _useConsoleLogging = true;
    private string? _logFilePath = "mcp-server.log";
    private Func<IServerTransport>? _transportFactory;
    private Assembly? _toolAssembly;
    internal readonly List<Assembly> _additionalToolAssemblies = new();
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

    /// <summary>
    /// Gets the authentication provider configured for this server
    /// </summary>
    public IAuthentication? Authentication => _authentication;

    /// <summary>
    /// Gets the API key validator configured for this server
    /// </summary>
    public IApiKeyValidator? ApiKeyValidator => _apiKeyValidator;

    /// <summary>
    /// Gets a value indicating whether SSE transport is being used
    /// </summary>
    public bool IsUsingSse => _useSse;

    /// <summary>
    /// Gets the port configured for the server
    /// </summary>
    public int Port => _serverConfiguration.Port;

    /// <summary>
    /// Gets the hostname configured for the server
    /// </summary>
    public string Hostname => _serverConfiguration.Hostname;

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
    /// Configure the server with a specific configuration object
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

    /// <summary>
    /// Configure the server to use API key authentication
    /// </summary>
    /// <param name="apiKey">The API key to authenticate with</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder UseApiKeyAuth(string apiKey)
    {
        _securityConfigured = true;
        var loggerFactory = _loggerFactory ?? CreateLoggerFactory();
        var logger = loggerFactory.CreateLogger<InMemoryApiKeyValidator>();
        var validator = new InMemoryApiKeyValidator(logger);
        // Add the provided API key for a default user
        validator.AddApiKey(apiKey, "default-user");
        return UseApiKeyAuth(validator);
    }

    /// <summary>
    /// Configure the server to use API key authentication with multiple valid keys
    /// </summary>
    /// <param name="apiKeys">Collection of valid API keys</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder UseApiKeyAuth(IEnumerable<string> apiKeys)
    {
        _securityConfigured = true;
        var loggerFactory = _loggerFactory ?? CreateLoggerFactory();
        var logger = loggerFactory.CreateLogger<InMemoryApiKeyValidator>();
        var validator = new InMemoryApiKeyValidator(logger);
        // Add all the provided API keys for default users
        int i = 0;
        foreach (var key in apiKeys)
        {
            validator.AddApiKey(key, $"default-user-{++i}");
        }
        return UseApiKeyAuth(validator);
    }

    /// <summary>
    /// Configure the server to use API key authentication with a custom validator
    /// </summary>
    /// <param name="validator">The custom validator to use</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder UseApiKeyAuth(IApiKeyValidator validator)
    {
        _securityConfigured = true;
        _apiKeyValidator = validator;
        return this;
    }

    /// <summary>
    /// Configure the server to use API key authentication with options
    /// </summary>
    /// <param name="configure">Action to configure the API key options</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder UseApiKeyAuthentication(Action<ApiKeyAuthOptions> configure)
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
        return this;
    }

    /// <summary>
    /// Configure the server to use a custom authentication mechanism
    /// </summary>
    /// <param name="authentication">The custom authentication mechanism</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder UseAuthentication(IAuthentication authentication)
    {
        _securityConfigured = true;
        _authentication = authentication;
        return this;
    }

    /// <summary>
    /// Configure the server to not use any authentication
    /// </summary>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder UseNoAuth()
    {
        _securityConfigured = true;
        _noAuthExplicitlyConfigured = true;
        return this;
    }

    /// <summary>
    /// Configure the server with a specific log level
    /// </summary>
    /// <param name="level">The log level to use</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder WithLogLevel(LogLevel level)
    {
        _logLevel = level;
        return this;
    }

    /// <summary>
    /// Configure the server with a specific log level (alias for WithLogLevel)
    /// </summary>
    /// <param name="level">The log level to use</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder UseLogLevel(LogLevel level)
    {
        return WithLogLevel(level);
    }

    /// <summary>
    /// Configure the server to use console logging
    /// </summary>
    /// <param name="useConsole">Whether to log to console</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder UseConsoleLogging(bool useConsole = true)
    {
        _useConsoleLogging = useConsole;
        return this;
    }

    /// <summary>
    /// Configure the server with a specific log file path
    /// </summary>
    /// <param name="filePath">The path to log to, or null to disable file logging</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder WithLogFile(string? filePath)
    {
        _logFilePath = filePath;
        return this;
    }

    /// <summary>
    /// Configure common log levels for specific components
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
    /// Configure the server to scan an assembly for tools
    /// </summary>
    /// <param name="assembly">The assembly to scan</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder ScanToolsFromAssembly(Assembly assembly)
    {
        _toolAssembly = assembly;
        return this;
    }

    /// <summary>
    /// Configure the server to scan an additional assembly for tools
    /// </summary>
    /// <param name="assembly">The additional assembly to scan</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder ScanAdditionalToolsFromAssembly(Assembly assembly)
    {
        _additionalToolAssemblies.Add(assembly);
        return this;
    }

    /// <summary>
    /// Configure the server to scan an additional assembly for tools (alias for ScanAdditionalToolsFromAssembly)
    /// </summary>
    /// <param name="assembly">The additional assembly to scan</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder WithAdditionalAssembly(Assembly assembly)
    {
        return ScanAdditionalToolsFromAssembly(assembly);
    }

    /// <summary>
    /// Register additional services for dependency injection
    /// </summary>
    /// <param name="configureServices">The action to configure services</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder ConfigureServices(Action<IServiceCollection> configureServices)
    {
        configureServices(_services);
        return this;
    }

    /// <summary>
    /// Register a specific logger factory
    /// </summary>
    /// <param name="loggerFactory">The logger factory to use</param>
    /// <returns>The builder for chaining</returns>
    public McpServerBuilder UseLoggerFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        return this;
    }

    /// <summary>
    /// Build and run the server asynchronously
    /// </summary>
    /// <returns>A task representing the running server</returns>
    public Task<McpServer> StartAsync()
    {
        return BuildAsync();
    }

    /// <summary>
    /// Build and run the server (async version)
    /// </summary>
    /// <returns>A task representing the running server</returns>
    public async Task<McpServer> BuildAsync()
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

        // Create server
        var server = new McpServer(_serverInfo, serverOptions, loggerFactory);

        // Set up authentication if needed
        if (_useSse && !_noAuthExplicitlyConfigured)
        {
            // Add default auth if SSE is used and no explicit auth is configured
            if (!_securityConfigured)
            {
                // TODO: add default authentication if needed
            }
            // Use explicit auth if provided
            else if (_authentication != null)
            {
                _services.AddSingleton(_authentication);
            }
            // Use API key auth if validator is provided
            else if (_apiKeyValidator != null)
            {
                _services.AddSingleton(_apiKeyValidator);
                _services.AddSingleton<IAuthentication>(provider =>
                {
                    var apiKeyOptions = new ApiKeyAuthOptions(); // Default options
                    return new ApiKeyAuthenticationHandler(
                        apiKeyOptions,
                        provider.GetRequiredService<IApiKeyValidator>(),
                        loggerFactory.CreateLogger<ApiKeyAuthenticationHandler>()
                    );
                });
            }
        }

        // Build service provider
        var serviceProvider = _services.BuildServiceProvider();

        // Register tools from entry assembly
        var entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly != null)
        {
            var logger = loggerFactory.CreateLogger(nameof(McpServerBuilder));
            logger.LogInformation(
                "Scanning entry assembly for tools: {AssemblyName}",
                entryAssembly.FullName
            );
            server.RegisterToolsFromAssembly(entryAssembly, serviceProvider);
        }

        // Register tools from explicitly specified assemblies
        if (_toolAssembly != null)
        {
            server.RegisterToolsFromAssembly(_toolAssembly, serviceProvider);
        }

        foreach (var assembly in _additionalToolAssemblies)
        {
            server.RegisterToolsFromAssembly(assembly, serviceProvider);
        }

        // Connect transport
        if (_transportFactory != null)
        {
            // Use the specified transport factory
            var transport = _transportFactory();
            await server.ConnectAsync(transport);
        }
        else if (_useSse)
        {
            // Set up and start SSE server
            var builder = new SseServerBuilder(loggerFactory);

            // Configure SseServerBuilder with McpServer
            // TODO: Add WithMcpServer method to SseServerBuilder

            // Configure base URL
            // TODO: Add UseBaseUrl method to SseServerBuilder

            // TODO: Add UseAuthentication method to SseServerBuilder

            // Start the SSE server
            // TODO: Add StartAsync method to SseServerBuilder
        }
        else
        {
            // Default to stdio transport
            var transport = new StdioTransport(
                Console.OpenStandardInput(),
                Console.OpenStandardOutput(),
                loggerFactory.CreateLogger<StdioTransport>()
            );
            await server.ConnectAsync(transport);
        }

        return server;
    }

    /// <summary>
    /// Build the server without connecting to a transport
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

        // Create server
        var server = new McpServer(_serverInfo, serverOptions, loggerFactory);

        // Build service provider
        var serviceProvider = _services.BuildServiceProvider();

        // Register tools from entry assembly
        var entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly != null)
        {
            var logger = loggerFactory.CreateLogger(nameof(McpServerBuilder));
            logger.LogInformation(
                "Scanning entry assembly for tools: {AssemblyName}",
                entryAssembly.FullName
            );
            server.RegisterToolsFromAssembly(entryAssembly, serviceProvider);
        }

        // Register tools from explicitly specified assemblies
        if (_toolAssembly != null)
        {
            server.RegisterToolsFromAssembly(_toolAssembly, serviceProvider);
        }

        foreach (var assembly in _additionalToolAssemblies)
        {
            server.RegisterToolsFromAssembly(assembly, serviceProvider);
        }

        return server;
    }

    /// <summary>
    /// Creates a new logger factory with configured settings
    /// </summary>
    private ILoggerFactory CreateLoggerFactory()
    {
        // If a logger factory was provided, use it
        if (_loggerFactory != null)
        {
            return _loggerFactory;
        }

        // Create options based on builder settings
        var options = new McpLoggingOptions
        {
            MinimumLogLevel = _logLevel,
            NoConsoleOutput = !_useConsoleLogging,
            LogFilePath = _logFilePath ?? "logs/mcp-server.log",
            UseStdio = !_useConsoleLogging,
        };

        // Create the configuration
        var configuration = new McpLoggingConfiguration(
            Microsoft.Extensions.Options.Options.Create(options)
        );

        // Create and return the logger factory
        return configuration.CreateLoggerFactory();
    }
}
