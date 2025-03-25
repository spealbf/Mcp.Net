using System.Reflection;
using System.Text.Json;
using Mcp.Net.Core.JsonRpc;
using Mcp.Net.Core.Models.Capabilities;
using Mcp.Net.Core.Models.Exceptions;
using Mcp.Net.Server;
using Mcp.Net.Server.Extensions;
using Mcp.Net.Server.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        builder.Services.AddSingleton<Mcp.Net.Server.SseConnectionManager>();
        builder.Services.AddCors();

        var app = builder.Build();

        // Register tools from assembly after the ServiceProvider is built
        mcpServer.RegisterToolsFromAssembly(Assembly.GetExecutingAssembly(), app.Services);
        var setupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Setup");
        setupLogger.LogInformation("Registered tools from assembly");

        app.UseCors(builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

        app.MapGet(
            "/sse",
            async (
                HttpContext context,
                McpServer server,
                Mcp.Net.Server.SseConnectionManager connectionManager,
                ILoggerFactory loggerFactory
            ) =>
            {
                var logger = loggerFactory.CreateLogger("SSE");
                string clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                logger.LogInformation("New SSE connection from {ClientIp}", clientIp);

                // Create HTTP response writer
                var responseWriter = new Mcp.Net.Server.HttpResponseWriter(
                    context.Response,
                    loggerFactory.CreateLogger<Mcp.Net.Server.HttpResponseWriter>()
                );

                // Create SSE transport with the response writer
                var transport = new Mcp.Net.Server.SseTransport(
                    responseWriter,
                    loggerFactory.CreateLogger<Mcp.Net.Server.SseTransport>()
                );

                // Register with connection manager
                connectionManager.RegisterTransport(transport);

                var sessionId = transport.SessionId;
                logger.LogInformation(
                    "Registered SSE transport with session ID {SessionId}",
                    sessionId
                );

                // Create a consistent log scope with session ID for correlation
                using (
                    logger.BeginScope(
                        new Dictionary<string, object>
                        {
                            ["SessionId"] = sessionId,
                            ["ClientIp"] = clientIp,
                        }
                    )
                )
                {
                    try
                    {
                        await server.ConnectAsync(transport);
                        logger.LogInformation("Server connected to transport");

                        // Keep the connection alive until client disconnects
                        await Task.Delay(-1, context.RequestAborted);
                    }
                    catch (TaskCanceledException)
                    {
                        logger.LogInformation("SSE connection closed");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error in SSE connection");
                    }
                    finally
                    {
                        await transport.CloseAsync();
                        logger.LogInformation("SSE transport closed");
                    }
                }
            }
        );

        app.MapPost(
            "/messages",
            async (
                HttpContext context,
                [FromServices] Mcp.Net.Server.SseConnectionManager connectionManager,
                ILoggerFactory loggerFactory
            ) =>
            {
                var logger = loggerFactory.CreateLogger("MessageEndpoint");
                var sessionId = context.Request.Query["sessionId"].ToString();
                if (string.IsNullOrEmpty(sessionId))
                {
                    logger.LogWarning("Message received without session ID");
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync(new { error = "Missing sessionId" });
                    return;
                }

                using (
                    logger.BeginScope(new Dictionary<string, object> { ["SessionId"] = sessionId })
                )
                {
                    var transport = connectionManager.GetTransport(sessionId);
                    if (transport == null)
                    {
                        logger.LogWarning("Session not found for ID {SessionId}", sessionId);
                        context.Response.StatusCode = 404;
                        await context.Response.WriteAsJsonAsync(
                            new { error = "Session not found" }
                        );
                        return;
                    }

                    try
                    {
                        var request = await JsonSerializer.DeserializeAsync<JsonRpcRequestMessage>(
                            context.Request.Body
                        );

                        if (request == null)
                        {
                            logger.LogError("Invalid JSON-RPC request format");
                            context.Response.StatusCode = 400;
                            await context.Response.WriteAsJsonAsync(
                                new JsonRpcError
                                {
                                    Code = (int)ErrorCode.InvalidRequest,
                                    Message = "Invalid request",
                                }
                            );
                            return;
                        }

                        // Use structured logging for the request
                        logger.LogDebug(
                            "JSON-RPC Request: Method={Method}, Id={Id}",
                            request.Method,
                            request.Id ?? "null"
                        );

                        if (request.Params != null)
                        {
                            logger.LogTrace(
                                "Request params: {Params}",
                                JsonSerializer.Serialize(request.Params)
                            );
                        }

                        // Process the request through the transport
                        transport.HandlePostMessage(request);

                        // Return 202 Accepted immediately
                        context.Response.StatusCode = 202;
                        await context.Response.WriteAsJsonAsync(new { status = "accepted" });
                    }
                    catch (JsonException ex)
                    {
                        logger.LogError(ex, "JSON parsing error");

                        try
                        {
                            // Rewind the request body stream to try to read it as raw data for debugging
                            context.Request.Body.Position = 0;
                            using var reader = new StreamReader(
                                context.Request.Body,
                                leaveOpen: true
                            );
                            string rawContent = await reader.ReadToEndAsync();

                            string truncatedContent =
                                rawContent.Length > 300
                                    ? rawContent.Substring(0, 297) + "..."
                                    : rawContent;

                            // Log the raw content
                            logger.LogDebug(
                                "Raw JSON content that failed parsing: {Content}",
                                truncatedContent
                            );

                            // Try to parse as generic JSON to extract useful information
                            try
                            {
                                var doc = JsonDocument.Parse(rawContent);
                                if (doc.RootElement.TryGetProperty("id", out var idElement))
                                {
                                    string idValue =
                                        idElement.ValueKind == JsonValueKind.Number
                                            ? idElement.GetRawText()
                                            : idElement.ToString();
                                    logger.LogInformation("Request had ID: {Id}", idValue);
                                }
                            }
                            catch (JsonException)
                            {
                                logger.LogError("Content is not valid JSON");
                            }

                            // Rewind again for any future operations
                            context.Request.Body.Position = 0;
                        }
                        catch (Exception logEx)
                        {
                            logger.LogError(logEx, "Failed to log request content");
                        }

                        context.Response.StatusCode = 400;
                        await context.Response.WriteAsJsonAsync(
                            new JsonRpcError
                            {
                                Code = (int)ErrorCode.ParseError,
                                Message = "Parse error",
                            }
                        );
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error processing message");
                        context.Response.StatusCode = 500;
                        await context.Response.WriteAsJsonAsync(
                            new JsonRpcError
                            {
                                Code = (int)ErrorCode.InternalError,
                                Message = "Internal server error",
                            }
                        );
                    }
                }
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
