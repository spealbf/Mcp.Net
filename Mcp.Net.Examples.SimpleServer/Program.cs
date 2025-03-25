using System;
using System.Threading;
using System.Threading.Tasks;
using Mcp.Net.Core.Models.Capabilities;
using Mcp.Net.Server.ServerBuilder;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.Examples.SimpleServer;

class Program
{
    static async Task Main(string[] args)
    {
        // Parse command line args
        bool useStdio = args.Length > 0 && args[0] == "--stdio";
        int port = ParsePort(args, 5000);

        // Set log level to Debug for better visibility
        LogLevel logLevel = LogLevel.Debug;

        // Display all registered tools at startup for easier debugging
        Environment.SetEnvironmentVariable("MCP_DEBUG_TOOLS", "true");

        if (useStdio)
        {
            await RunWithStdioTransport(logLevel);
        }
        else
        {
            await RunWithSseTransport(port, logLevel);
        }
    }

    /// <summary>
    /// Simple method to run the server with SSE transport
    /// </summary>
    static async Task RunWithSseTransport(int port, LogLevel logLevel)
    {
        Console.WriteLine($"Starting MCP server on port {port}...");

        var builder = WebApplication.CreateBuilder();

        // Configure logging
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(logLevel);

        // Add CORS for web clients
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
            });
        });

        // Add and configure MCP Server
        builder.Services.AddMcpServer(server =>
        {
            server
                .WithName("Simple MCP Server")
                .WithVersion("1.0.0")
                .WithInstructions("Example server with calculator and Warhammer 40k tools")
                .UseLogLevel(logLevel)
                .UsePort(port)
                .UseSseTransport()
                .ConfigureCommonLogLevels(
                    toolsLevel: LogLevel.Debug,
                    transportLevel: LogLevel.Debug,
                    jsonRpcLevel: LogLevel.Debug
                );
        });

        var app = builder.Build();

        // Configure middleware
        app.UseCors();
        app.UseMcpServer();

        // Display the server URL
        var config = app.Services.GetRequiredService<McpServerConfiguration>();
        Console.WriteLine($"Server started at http://{config.Hostname}:{config.Port}");
        Console.WriteLine("Press Ctrl+C to stop the server.");

        // Start the server
        await app.RunAsync($"http://localhost:{port}");
    }

    /// <summary>
    /// Run the server with stdio transport
    /// </summary>
    static async Task RunWithStdioTransport(LogLevel logLevel)
    {
        // Create and start the MCP server with stdio transport
        var server = await new Server.ServerBuilder.McpServerBuilder()
            .WithName("Simple MCP Server")
            .WithVersion("1.0.0")
            .WithInstructions("Example server with calculator and Warhammer 40k tools")
            .UseLogLevel(logLevel)
            .UseStdioTransport()
            .ConfigureCommonLogLevels(
                toolsLevel: LogLevel.Debug,
                transportLevel: LogLevel.Debug,
                jsonRpcLevel: LogLevel.Debug
            )
            .StartAsync();

        // Wait for Ctrl+C to exit
        var tcs = new TaskCompletionSource<bool>();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            tcs.TrySetResult(true);
        };
        await tcs.Task;
    }

    /// <summary>
    /// Parse port from command line args or environment variables
    /// </summary>
    static int ParsePort(string[] args, int defaultPort)
    {
        // Check environment variable first
        string? envPort = Environment.GetEnvironmentVariable("MCP_PORT");
        if (envPort != null && int.TryParse(envPort, out int parsedEnvPort))
        {
            return parsedEnvPort;
        }

        // Then check command line args
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--port" && int.TryParse(args[i + 1], out int parsedPort))
            {
                return parsedPort;
            }
        }

        return defaultPort;
    }
}
