using Mcp.Net.Core.Transport;
using Mcp.Net.Server.Authentication;
using Mcp.Net.Server.ConnectionManagers;
using Mcp.Net.Server.Extensions;
using Mcp.Net.Server.Interfaces;
using Mcp.Net.Server.Transport.Sse;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SseConnectionManagerType = Mcp.Net.Server.Transport.Sse.SseConnectionManager;

namespace Mcp.Net.Server.ServerBuilder;

/// <summary>
/// Builder for creating and configuring an SSE-based MCP server.
/// </summary>
public class SseServerBuilder : IMcpServerBuilder, ITransportBuilder
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<SseServerBuilder> _logger;
    private string _hostname = "localhost";
    private int _port = 5000;
    private IAuthentication? _authentication;
    private IApiKeyValidator? _apiKeyValidator;
    private string[] _args = Array.Empty<string>();
    private readonly Dictionary<string, string> _customSettings = new();
    private IConfiguration? _configuration;

    /// <summary>
    /// Gets the hostname for the SSE server.
    /// </summary>
    public string Hostname => _hostname;

    /// <summary>
    /// Gets the port for the SSE server.
    /// </summary>
    public int HostPort => _port;

    /// <summary>
    /// Gets the base URL for the SSE server.
    /// </summary>
    public string BaseUrl => $"http://{_hostname}:{_port}";

    /// <summary>
    /// Initializes a new instance of the <see cref="SseServerBuilder"/> class.
    /// </summary>
    /// <param name="loggerFactory">Logger factory for creating loggers</param>
    public SseServerBuilder(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = _loggerFactory.CreateLogger<SseServerBuilder>();
    }

    /// <summary>
    /// Configures the SSE server with a specific hostname.
    /// </summary>
    /// <param name="hostname">The hostname to use</param>
    /// <returns>The builder for chaining</returns>
    public SseServerBuilder UseHostname(string hostname)
    {
        _hostname = hostname ?? throw new ArgumentNullException(nameof(hostname));
        return this;
    }

    /// <summary>
    /// Configures the SSE server with a specific port.
    /// </summary>
    /// <param name="port">The port to use</param>
    /// <returns>The builder for chaining</returns>
    public SseServerBuilder UsePort(int port)
    {
        if (port <= 0)
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be greater than zero");
            
        _port = port;
        return this;
    }

    /// <summary>
    /// Configures the SSE server with authentication.
    /// </summary>
    /// <param name="authentication">The authentication mechanism to use</param>
    /// <returns>The builder for chaining</returns>
    public SseServerBuilder UseAuthentication(IAuthentication authentication)
    {
        _authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        return this;
    }

    /// <summary>
    /// Configures the SSE server with API key authentication.
    /// </summary>
    /// <param name="validator">The API key validator to use</param>
    /// <returns>The builder for chaining</returns>
    public SseServerBuilder UseApiKeyAuthentication(IApiKeyValidator validator)
    {
        _apiKeyValidator = validator ?? throw new ArgumentNullException(nameof(validator));
        return this;
    }

    /// <summary>
    /// Configures the SSE server with command-line arguments.
    /// </summary>
    /// <param name="args">The command-line arguments</param>
    /// <returns>The builder for chaining</returns>
    public SseServerBuilder WithArgs(string[] args)
    {
        _args = args ?? Array.Empty<string>();
        return this;
    }

    /// <summary>
    /// Adds a custom setting for the SSE server.
    /// </summary>
    /// <param name="key">The setting key</param>
    /// <param name="value">The setting value</param>
    /// <returns>The builder for chaining</returns>
    public SseServerBuilder WithSetting(string key, string value)
    {
        _customSettings[key] = value;
        return this;
    }

    /// <inheritdoc />
    public McpServer Build()
    {
        // This should be handled by the main McpServerBuilder
        throw new InvalidOperationException("SseServerBuilder doesn't implement Build directly. Use McpServerBuilder instead.");
    }

    /// <inheritdoc />
    public async Task StartAsync(McpServer server)
    {
        if (server == null)
            throw new ArgumentNullException(nameof(server));

        _logger.LogInformation("Starting MCP server with SSE transport on {BaseUrl}", BaseUrl);

        var webApp = ConfigureWebApplication(_args, server);

        var serverConfig = ConfigureServerEndpoints(webApp, _args);

        _logger.LogInformation("Starting web server on {ServerUrl}", serverConfig.ServerUrl);
        
        // Start the web server
        await webApp.StartAsync();
        
        _logger.LogInformation("SSE server started successfully");
        
        // Create a task that completes when the application is stopped
        var shutdownTcs = new TaskCompletionSource<bool>();
        webApp.Lifetime.ApplicationStopping.Register(() => shutdownTcs.TrySetResult(true));
        
        // Wait for application to stop
        await shutdownTcs.Task;
    }

    /// <inheritdoc />
    public IServerTransport BuildTransport()
    {
        // SSE transport is built and managed by ASP.NET Core integration
        throw new InvalidOperationException("SSE transport is built and managed by ASP.NET Core integration");
    }

    /// <summary>
    /// Configures the web application with services and endpoints.
    /// </summary>
    private WebApplication ConfigureWebApplication(string[] args, McpServer server)
    {
        var builder = WebApplication.CreateBuilder(args);

        ConfigureAppSettings(builder, args);

        ConfigureWebAppLogging(builder);

        ConfigureMcpServices(builder, server);

        var app = builder.Build();

        ConfigureMiddleware(app);
        ConfigureEndpoints(app);

        return app;
    }

    /// <summary>
    /// Configures application settings from various sources.
    /// </summary>
    private void ConfigureAppSettings(WebApplicationBuilder builder, string[] args)
    {
        // Add configuration from multiple sources with priority:
        // 1. Command line args (highest)
        // 2. Environment variables
        // 3. appsettings.json (lowest)
        builder.Configuration.AddJsonFile("appsettings.json", optional: true);
        builder.Configuration.AddEnvironmentVariables("MCP_");
        builder.Configuration.AddCommandLine(args);
        
        // Add custom settings
        foreach (var setting in _customSettings)
        {
            builder.Configuration[setting.Key] = setting.Value;
        }
    }

    /// <summary>
    /// Configures logging for the web application.
    /// </summary>
    private void ConfigureWebAppLogging(WebApplicationBuilder builder)
    {
        // Use our custom logger factory
        builder.Logging.ClearProviders();
        builder.Logging.Services.AddSingleton(_loggerFactory);
    }

    /// <summary>
    /// Configures MCP services for the web application.
    /// </summary>
    private void ConfigureMcpServices(WebApplicationBuilder builder, McpServer server)
    {
        // Register server in DI
        builder.Services.AddSingleton(server);

        // Register connection managers
        builder.Services.AddSingleton<IConnectionManager, InMemoryConnectionManager>(
            provider => new InMemoryConnectionManager(_loggerFactory, TimeSpan.FromMinutes(30))
        );

        builder.Services.AddSingleton<SseConnectionManagerType>(provider => new SseConnectionManagerType(
            server,
            _loggerFactory,
            TimeSpan.FromMinutes(30)
        ));

        // Register authentication if configured
        if (_authentication != null)
        {
            builder.Services.AddSingleton(_authentication);
        }
        else if (_apiKeyValidator != null)
        {
            builder.Services.AddSingleton(_apiKeyValidator);
            builder.Services.AddSingleton<IAuthentication>(provider =>
            {
                var options = new ApiKeyAuthOptions
                {
                    HeaderName = "X-API-Key",
                    QueryParamName = "api_key"
                };
                
                return new ApiKeyAuthenticationHandler(
                    options,
                    _apiKeyValidator,
                    _loggerFactory.CreateLogger<ApiKeyAuthenticationHandler>()
                );
            });
        }

        // Add health checks
        builder.Services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy());

        // Add additional services
        builder.Services.AddCors();
        
        // Register server configuration
        var logger = _loggerFactory.CreateLogger("ServerConfig");
        
        // Use the builder's configuration or create a new one
        var config = _configuration ?? builder.Configuration;
        builder.Services.AddSingleton(
            new Server.ServerConfiguration(config, logger)
        );
    }

    /// <summary>
    /// Configures middleware for the web application.
    /// </summary>
    private void ConfigureMiddleware(WebApplication app)
    {
        // Configure CORS
        app.UseCors(builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

        // Add health check endpoints
        app.UseHealthChecks("/health");
        app.UseHealthChecks(
            "/health/ready",
            new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }
        );
        app.UseHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => true });

        // Use MCP server middleware
        app.UseMcpServer();
    }

    /// <summary>
    /// Configures endpoints for the web application.
    /// </summary>
    private void ConfigureEndpoints(WebApplication app)
    {
        // SSE and message endpoints are configured by UseMcpServer middleware
        _logger.LogDebug("SSE endpoints configured");
    }

    /// <summary>
    /// Configures server endpoints using the configuration.
    /// </summary>
    private Server.ServerConfiguration ConfigureServerEndpoints(WebApplication app, string[] args)
    {
        var serverLogger = _loggerFactory.CreateLogger("ServerConfig");

        // Store configuration for later use
        _configuration = app.Configuration;

        // Configure server using the dedicated configuration class
        var serverConfig = new Server.ServerConfiguration(app.Configuration, serverLogger);
        
        // Update the configuration with our stored values
        serverConfig.Configure(args);
        
        return serverConfig;
    }
}
