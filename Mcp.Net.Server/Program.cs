using System.Reflection;
using Mcp.Net.Core.Models.Capabilities;
using Mcp.Net.Server;
using Mcp.Net.Server.Extensions;
using Mcp.Net.Server.Logging;
using Microsoft.AspNetCore.Mvc;
using Serilog;

public static class Program
{
    public static async Task Main(string[] args)
    {
        bool useStdio = args.Contains("--stdio") || args.Contains("-s");
        bool debugMode = args.Contains("--debug") || args.Contains("-d");
        string logPath = GetArgumentValue(args, "--log-path") ?? "mcp-server.log";

        // Initialize logger with expanded configuration
        McpLoggerConfiguration.Instance.Configure(
            new McpLoggerOptions
            {
                UseStdio = useStdio,
                MinimumLogLevel = debugMode ? LogLevel.Debug : LogLevel.Information,
                LogFilePath = logPath,
                // If using stdio, don't write logs to the console
                NoConsoleOutput = useStdio,
                // Set sensible defaults for file rotation
                FileRollingInterval = RollingInterval.Day,
                FileSizeLimitBytes = 10 * 1024 * 1024, // 10MB
                RetainedFileCountLimit = 31, // Keep a month of logs
                PrettyConsoleOutput = true,
            }
        );

        var initialLogger = McpLoggerConfiguration
            .Instance.CreateLoggerFactory()
            .CreateLogger("Startup");
        initialLogger.LogInformation(
            "MCP Server starting. UseStdio={UseStdio}, DebugMode={DebugMode}, LogPath={LogPath}",
            useStdio,
            debugMode,
            logPath
        );

        if (useStdio)
        {
            await RunWithStdioTransport();
        }
        else
        {
            RunWithSseTransport(args);
        }
    }

    /// <summary>
    /// Gets the value of a command-line argument
    /// </summary>
    /// <param name="args">Array of command-line arguments</param>
    /// <param name="argName">Name of the argument to find</param>
    /// <returns>The value of the argument, or null if not found</returns>
    private static string? GetArgumentValue(string[] args, string argName)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == argName)
            {
                return args[i + 1];
            }
        }
        return null;
    }

    private static void RunWithSseTransport(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Provide configuration option via appsettings.json
        builder.Configuration.AddJsonFile("appsettings.json", optional: true);
        builder.Configuration.AddEnvironmentVariables("MCP_");
        builder.Configuration.AddCommandLine(args);

        // Use our custom logger factory
        builder.Logging.ClearProviders();
        builder.Logging.Services.AddSingleton<ILoggerFactory>(
            McpLoggerConfiguration.Instance.CreateLoggerFactory()
        );

        // Configure MCP Server
        var mcpServer = new McpServer(
            new ServerInfo { Name = "example-server", Version = "1.0.0" },
            new ServerOptions
            {
                Capabilities = new ServerCapabilities { Tools = new { } },
                Instructions = "This server provides dynamic tools",
            }
        );

        // Register server in DI
        builder.Services.AddSingleton(mcpServer);
        builder.Services.AddSingleton<SseConnectionManager>(provider => new SseConnectionManager(
            mcpServer,
            McpLoggerConfiguration.Instance.CreateLoggerFactory()
        ));
        builder.Services.AddCors();

        var app = builder.Build();

        // Register tools from assembly after the ServiceProvider is built
        mcpServer.RegisterToolsFromAssembly(Assembly.GetExecutingAssembly(), app.Services);
        var setupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Setup");
        setupLogger.LogInformation("Registered tools from assembly");

        app.UseCors(builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

        app.MapGet(
            "/sse",
            async (HttpContext context, SseConnectionManager connectionManager) =>
            {
                await connectionManager.HandleSseConnectionAsync(context);
            }
        );

        app.MapPost(
            "/messages",
            async (HttpContext context, [FromServices] SseConnectionManager connectionManager) =>
            {
                await connectionManager.HandleMessageAsync(context);
            }
        );

        var logFactory = McpLoggerConfiguration.Instance.CreateLoggerFactory();
        var serverLogger = logFactory.CreateLogger("WebServer");

        // Configure server using the dedicated configuration class
        var serverConfig = new ServerConfiguration(builder.Configuration, serverLogger);
        serverConfig.Configure(args);

        // Start the web server with the configured URL
        serverLogger.LogInformation("Starting web server on {ServerUrl}", serverConfig.ServerUrl);
        app.Run(serverConfig.ServerUrl);
    }

    private static async Task RunWithStdioTransport()
    {
        var loggerFactory = McpLoggerConfiguration.Instance.CreateLoggerFactory();
        var logger = loggerFactory.CreateLogger("StdioServer");

        logger.LogInformation("Starting MCP server with stdio transport");

        var mcpServer = new McpServer(
            new ServerInfo { Name = "example-server", Version = "1.0.0" },
            new ServerOptions
            {
                Capabilities = new ServerCapabilities { Tools = new { } },
                Instructions = "This server provides dynamic tools",
            },
            loggerFactory
        );

        var services = new ServiceCollection()
            .AddSingleton(mcpServer)
            .AddSingleton<ILoggerFactory>(loggerFactory)
            .BuildServiceProvider();

        mcpServer.RegisterToolsFromAssembly(Assembly.GetExecutingAssembly(), services);
        logger.LogInformation("Registered tools from assembly");

        var transport = new StdioTransport(
            Console.OpenStandardInput(),
            Console.OpenStandardOutput(),
            loggerFactory.CreateLogger<StdioTransport>()
        );

        // Handle transport events
        transport.OnRequest += request =>
        {
            logger.LogDebug(
                "JSON-RPC Request: Method={Method}, Id={Id}",
                request.Method,
                request.Id ?? "null"
            );
        };

        transport.OnError += ex =>
        {
            logger.LogError(ex, "Stdio transport error");
        };

        transport.OnClose += () =>
        {
            logger.LogInformation("Stdio transport closed");
        };

        await mcpServer.ConnectAsync(transport);
        logger.LogInformation("Server connected to stdio transport");

        // Keep running until transport is closed
        var tcs = new TaskCompletionSource<bool>();
        transport.OnClose += () => tcs.TrySetResult(true);

        logger.LogInformation("MCP server running with stdio transport");
        await tcs.Task;
    }
}
