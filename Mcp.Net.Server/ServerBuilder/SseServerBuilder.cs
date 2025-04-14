using System.Reflection;
using Mcp.Net.Core.Models.Capabilities;
using Mcp.Net.Server.ConnectionManagers;
using Mcp.Net.Server.Extensions;
using Mcp.Net.Server.Interfaces;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Mcp.Net.Server.ServerBuilder;

/// <summary>
/// Builder for creating and configuring an SSE-based MCP server
/// </summary>
public class SseServerBuilder
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private McpServer? _mcpServer;
    private string _baseUrl = "http://localhost:5000";
    private bool _useAuthentication = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="SseServerBuilder"/> class
    /// </summary>
    /// <param name="loggerFactory">Logger factory for creating loggers</param>
    public SseServerBuilder(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = _loggerFactory.CreateLogger<SseServerBuilder>();
    }

    /// <summary>
    /// Configures the SSE server with the MCP server
    /// </summary>
    /// <param name="server">The MCP server to use</param>
    /// <returns>The builder for chaining</returns>
    public SseServerBuilder WithMcpServer(McpServer server)
    {
        _mcpServer = server ?? throw new ArgumentNullException(nameof(server));
        return this;
    }

    /// <summary>
    /// Configures the SSE server with a specific base URL
    /// </summary>
    /// <param name="baseUrl">The base URL to use</param>
    /// <returns>The builder for chaining</returns>
    public SseServerBuilder UseBaseUrl(string baseUrl)
    {
        _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        return this;
    }

    /// <summary>
    /// Configures the SSE server to use authentication
    /// </summary>
    /// <returns>The builder for chaining</returns>
    public SseServerBuilder UseAuthentication()
    {
        _useAuthentication = true;
        return this;
    }

    /// <summary>
    /// Starts the SSE server asynchronously
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task StartAsync()
    {
        if (_mcpServer == null)
        {
            throw new InvalidOperationException("MCP server must be configured before starting");
        }

        _logger.LogInformation("Starting SSE server on base URL: {BaseUrl}", _baseUrl);

        // TODO: Implement the actual SSE server startup logic
        // This would typically:
        // 1. Configure the SSE endpoints
        // 2. Start a web server
        // 3. Connect the MCP server to the SSE transport

        // Use authentication if configured
        if (_useAuthentication)
        {
            _logger.LogInformation("SSE server will use authentication");
            // TODO: Set up authentication middleware
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Configures and runs an SSE-based MCP server
    /// </summary>
    /// <param name="args">Command-line arguments</param>
    public void Run(string[] args)
    {
        _logger.LogInformation("Starting MCP server with SSE transport");

        var webApp = ConfigureWebApplication(args);

        var serverConfig = ConfigureServerEndpoints(webApp, args);

        _logger.LogInformation("Starting web server on {ServerUrl}", serverConfig.ServerUrl);
        webApp.Run(serverConfig.ServerUrl);
    }

    /// <summary>
    /// Configures the web application with services and endpoints
    /// </summary>
    private WebApplication ConfigureWebApplication(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        ConfigureAppSettings(builder, args);

        ConfigureWebAppLogging(builder);

        ConfigureMcpServices(builder);

        var app = builder.Build();

        var mcpServer = app.Services.GetRequiredService<McpServer>();
        mcpServer.RegisterToolsFromAssembly(Assembly.GetExecutingAssembly(), app.Services);

        var setupLogger = _loggerFactory.CreateLogger("Setup");
        setupLogger.LogInformation("Registered tools from assembly");

        ConfigureMiddleware(app);
        ConfigureEndpoints(app);

        return app;
    }

    /// <summary>
    /// Configures application settings from various sources
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
    }

    /// <summary>
    /// Configures logging for the web application
    /// </summary>
    private void ConfigureWebAppLogging(WebApplicationBuilder builder)
    {
        // Use our custom logger factory
        builder.Logging.ClearProviders();
        builder.Logging.Services.AddSingleton(_loggerFactory);
    }

    /// <summary>
    /// Configures MCP services for the web application
    /// </summary>
    private void ConfigureMcpServices(WebApplicationBuilder builder)
    {
        // Create and register MCP server
        var mcpServer = CreateMcpServer();

        // Register server in DI
        builder.Services.AddSingleton(mcpServer);

        // Register connection managers
        builder.Services.AddSingleton<IConnectionManager, InMemoryConnectionManager>(
            provider => new InMemoryConnectionManager(_loggerFactory, TimeSpan.FromMinutes(30))
        );

        builder.Services.AddSingleton<SseConnectionManager>(provider => new SseConnectionManager(
            mcpServer,
            _loggerFactory,
            TimeSpan.FromMinutes(30)
        ));

        // Add hosted service for graceful shutdown
        builder.Services.AddHostedService<GracefulShutdownService>();

        // Add health checks
        builder.Services.AddHealthChecks().AddCheck("self", () => HealthCheckResult.Healthy());

        // Add additional services
        builder.Services.AddCors();
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
    /// Configures middleware for the web application
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

        // Additional middleware can be added here
    }

    /// <summary>
    /// Configures endpoints for the web application
    /// </summary>
    private void ConfigureEndpoints(WebApplication app)
    {
        // SSE endpoint for establishing connections
        app.MapGet(
            "/sse",
            async (HttpContext context, SseConnectionManager connectionManager) =>
            {
                await connectionManager.HandleSseConnectionAsync(context);
            }
        );

        // Message endpoint for receiving client messages
        app.MapPost(
            "/messages",
            async (HttpContext context, [FromServices] SseConnectionManager connectionManager) =>
            {
                await connectionManager.HandleMessageAsync(context);
            }
        );
    }

    /// <summary>
    /// Configures server endpoints using the configuration
    /// </summary>
    private ServerConfiguration ConfigureServerEndpoints(WebApplication app, string[] args)
    {
        var serverLogger = _loggerFactory.CreateLogger("ServerConfig");

        // Configure server using the dedicated configuration class
        var serverConfig = new ServerConfiguration(app.Configuration, serverLogger);
        serverConfig.Configure(args);

        return serverConfig;
    }
}
