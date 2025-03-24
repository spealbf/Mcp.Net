using System.Reflection;
using System.Text.Json;
using Mcp.Net.Core.JsonRpc;
using Mcp.Net.Core.Models.Capabilities;
using Mcp.Net.Core.Models.Exceptions;
using Mcp.Net.Server;
using Mcp.Net.Server.Extensions;
using Mcp.Net.Server.Logging;
using Microsoft.AspNetCore.Mvc;

public static class Program
{
    public static async Task Main(string[] args)
    {
        bool useStdio = args.Contains("--stdio") || args.Contains("-s");
        bool debugMode = args.Contains("--debug") || args.Contains("-d");
        string logPath = GetArgumentValue(args, "--log-path") ?? "mcp-server.log";

        // Initialize logger - Redirect to file if using stdio
        Logger.Initialize(
            new LoggerOptions
            {
                UseStdio = useStdio,
                DebugMode = debugMode,
                LogFilePath = logPath,
                // If using stdio, don't write logs to the console
                NoConsoleOutput = useStdio,
            }
        );

        Logger.Information(
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

        // Suppress default logging
        builder.Logging.ClearProviders();

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
        Logger.Information("Registered tools from assembly");

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

                using (logger.BeginScope("SessionId: {SessionId}", sessionId))
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
                [FromServices] Mcp.Net.Server.SseConnectionManager connectionManager
            ) =>
            {
                var sessionId = context.Request.Query["sessionId"].ToString();
                if (string.IsNullOrEmpty(sessionId))
                {
                    Logger.Warning("Message received without session ID");
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync(new { error = "Missing sessionId" });
                    return;
                }

                using (Logger.BeginScope(sessionId))
                {
                    var transport = connectionManager.GetTransport(sessionId);
                    if (transport == null)
                    {
                        Logger.Warning("Session not found for ID {SessionId}", sessionId);
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
                            Logger.Error("Invalid JSON-RPC request format");
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

                        Logger.LogRequest(sessionId, request.Method, request.Id, request.Params);

                        // Process the request through the transport
                        transport.HandlePostMessage(request);

                        // Return 202 Accepted immediately
                        context.Response.StatusCode = 202;
                        await context.Response.WriteAsJsonAsync(new { status = "accepted" });
                    }
                    catch (JsonException ex)
                    {
                        Logger.Error("JSON parsing error", ex);

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
                            Logger.Information(
                                $"Raw JSON content that failed parsing: {truncatedContent}"
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
                                    Logger.Information($"Request had ID: {idValue}");
                                }
                            }
                            catch (JsonException)
                            {
                                Logger.Error("Content is not valid JSON");
                            }

                            // Rewind again for any future operations
                            context.Request.Body.Position = 0;
                        }
                        catch (Exception logEx)
                        {
                            Logger.Error("Failed to log request content", logEx);
                        }

                        context.Response.StatusCode = 400;
                        await context.Response.WriteAsJsonAsync(
                            new JsonRpcError
                            {
                                Code = (int)ErrorCode.ParseError,
                                Message = $"Parse error: {ex.Message}",
                            }
                        );
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error processing message", ex);
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

        Logger.Information("Starting web server on http://localhost:5000");
        app.Run("http://localhost:5000");
    }

    private static async Task RunWithStdioTransport()
    {
        Logger.Information("Starting MCP server with stdio transport");

        var mcpServer = new McpServer(
            new ServerInfo { Name = "example-server", Version = "1.0.0" },
            new ServerOptions
            {
                Capabilities = new ServerCapabilities { Tools = new { } },
                Instructions = "This server provides dynamic tools",
            }
        );

        var services = new ServiceCollection().AddSingleton(mcpServer).BuildServiceProvider();

        mcpServer.RegisterToolsFromAssembly(Assembly.GetExecutingAssembly(), services);
        Logger.Information("Registered tools from assembly");

        var transport = new StdioTransport(
            Console.OpenStandardInput(),
            Console.OpenStandardOutput()
        );

        // Handle transport events
        transport.OnRequest += request =>
        {
            Logger.LogRequest("stdio", request.Method, request.Id, request.Params);
        };

        transport.OnError += ex =>
        {
            Logger.Error("Stdio transport error", ex);
        };

        transport.OnClose += () =>
        {
            Logger.Information("Stdio transport closed");
        };

        await mcpServer.ConnectAsync(transport);
        Logger.Information("Server connected to stdio transport");

        // Keep running until transport is closed
        var tcs = new TaskCompletionSource<bool>();
        transport.OnClose += () => tcs.TrySetResult(true);

        Logger.Information("MCP server running with stdio transport");
        await tcs.Task;
    }
}
