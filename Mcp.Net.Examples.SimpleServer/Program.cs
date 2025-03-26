using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mcp.Net.Core.Models.Capabilities;
using Mcp.Net.Server.Authentication;
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

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(logLevel);

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
            });
        });

        string hostname =
            Environment.GetEnvironmentVariable("PORT") != null ? "0.0.0.0" : "localhost";

        builder.Services.AddHealthChecks();

        // Add and configure MCP Server
        builder.Services.AddMcpServer(server =>
        {
            server
                .WithName("Simple MCP Server")
                .WithVersion("1.0.0")
                .WithInstructions("Example server with calculator and Warhammer 40k tools")
                .UseLogLevel(logLevel)
                .UsePort(port)
                .UseHostname(hostname)
                .UseSseTransport()
                // Load tools from the external tools assembly, in addition to the entry assembly
                .WithAdditionalAssembly(
                    typeof(Mcp.Net.Examples.ExternalTools.UtilityTools).Assembly
                )
                .ConfigureCommonLogLevels(
                    toolsLevel: LogLevel.Debug,
                    transportLevel: LogLevel.Debug,
                    jsonRpcLevel: LogLevel.Debug
                )
                // Add API key authentication
                .UseApiKeyAuthentication(options =>
                {
                    options.HeaderName = "X-API-Key";
                    options.QueryParamName = "api_key";
                });
        });

        var app = builder.Build();

        // Configure middleware
        app.UseCors();
        app.UseHealthChecks("/health");
        Console.WriteLine("Health check endpoint enabled at /health");

        // Add API keys to the validator
        var apiKeyValidator = app.Services.GetService<IApiKeyValidator>();
        if (apiKeyValidator is InMemoryApiKeyValidator validator)
        {
            // Add some API keys
            validator.AddApiKey(
                "test-key-123",
                "user1",
                new Dictionary<string, string> { ["role"] = "admin" }
            );

            validator.AddApiKey(
                "demo-key-456",
                "user2",
                new Dictionary<string, string> { ["role"] = "user" }
            );

            Console.WriteLine("Added API keys for authentication:");
            Console.WriteLine("  - test-key-123 (user1, admin)");
            Console.WriteLine("  - demo-key-456 (user2, user)");
        }

        app.UseMcpServer();

        // Display the server URL
        var config = app.Services.GetRequiredService<McpServerConfiguration>();
        Console.WriteLine($"Server started at http://{config.Hostname}:{config.Port}");
        Console.WriteLine("Press Ctrl+C to stop the server.");

        await app.RunAsync($"http://{hostname}:{port}");
    }

    /// <summary>
    /// Run the server with stdio transport
    /// </summary>
    static async Task RunWithStdioTransport(LogLevel logLevel)
    {
        var server = await new Server.ServerBuilder.McpServerBuilder()
            .WithName("Simple MCP Server")
            .WithVersion("1.0.0")
            .WithInstructions("Example server with calculator and Warhammer 40k tools")
            .UseLogLevel(logLevel)
            .UseStdioTransport()
            // Load tools from the external tools assembly, in addition to the entry assembly
            .WithAdditionalAssembly(typeof(Mcp.Net.Examples.ExternalTools.UtilityTools).Assembly)
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
    /// Parse port from environment variable or command line args
    /// </summary>
    static int ParsePort(string[] args, int defaultPort)
    {
        string? envPort = Environment.GetEnvironmentVariable("PORT");
        if (envPort != null && int.TryParse(envPort, out int parsedEnvPort))
        {
            Console.WriteLine($"Using PORT environment variable: {parsedEnvPort}");
            return parsedEnvPort;
        }

        // Then check command line args
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--port" && int.TryParse(args[i + 1], out int parsedPort))
            {
                Console.WriteLine($"Using command line port: {parsedPort}");
                return parsedPort;
            }
        }

        Console.WriteLine($"Using default port: {defaultPort}");
        return defaultPort;
    }
}
