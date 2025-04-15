using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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
        // Create a CommandLineOptions object to parse args
        var options = CommandLineOptions.Parse(args);

        // Set log level to Debug for better visibility
        LogLevel logLevel = LogLevel.Debug;

        // Display all registered tools at startup for easier debugging
        Environment.SetEnvironmentVariable("MCP_DEBUG_TOOLS", "true");

        if (options.UseStdio)
        {
            await RunWithStdioTransport(options, logLevel);
        }
        else
        {
            await RunWithSseTransport(options, logLevel);
        }
    }

    /// <summary>
    /// Simple method to run the server with SSE transport
    /// </summary>
    static async Task RunWithSseTransport(CommandLineOptions options, LogLevel logLevel)
    {
        int port = options.Port ?? 5000;
        Console.WriteLine($"Starting MCP server on port {port}...");

        var builder = WebApplication.CreateBuilder();

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(logLevel);

        string hostname =
            options.Hostname
            ?? (Environment.GetEnvironmentVariable("PORT") != null ? "0.0.0.0" : "localhost");

        builder.Services.AddHealthChecks();

        // Add and configure MCP Server with the updated API
        builder.Services.AddMcpServer(mcpBuilder =>
        {
            // Configure the provided builder - DON'T create a new one!
            mcpBuilder
                .WithName(options.ServerName ?? "Simple MCP Server")
                .WithVersion("1.0.0")
                .WithInstructions("Example server with calculator and Warhammer 40k tools")
                .WithLogLevel(logLevel)
                .WithPort(port)
                .WithHostname(hostname);

            // Add the entry assembly to scan for tools
            Assembly? entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly != null)
            {
                Console.WriteLine($"Scanning entry assembly for tools: {entryAssembly.FullName}");
            }

            // Add external tools assembly
            var externalToolsAssembly =
                typeof(Mcp.Net.Examples.ExternalTools.UtilityTools).Assembly;
            Console.WriteLine(
                $"Adding external tools assembly: {externalToolsAssembly.GetName().Name}"
            );
            mcpBuilder.WithAdditionalAssembly(externalToolsAssembly);

            // Add custom tool assemblies if specified
            if (options.ToolAssemblies != null)
            {
                foreach (var assemblyPath in options.ToolAssemblies)
                {
                    try
                    {
                        var assembly = Assembly.LoadFrom(assemblyPath);
                        mcpBuilder.WithAdditionalAssembly(assembly);
                        Console.WriteLine($"Added tool assembly: {assembly.GetName().Name}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to load assembly {assemblyPath}: {ex.Message}");
                    }
                }
            }

            // Configure common log levels
            mcpBuilder.ConfigureCommonLogLevels(
                toolsLevel: LogLevel.Debug,
                transportLevel: LogLevel.Debug,
                jsonRpcLevel: LogLevel.Debug
            );

            // Add API key authentication
            mcpBuilder.WithApiKey("test-key-123"); // Default API key
        });

        var app = builder.Build();

        // Add API keys to the validator
        var apiKeyValidator = app.Services.GetService<IApiKeyValidator>();
        if (apiKeyValidator is InMemoryApiKeyValidator validator)
        {
            // Add additional API keys
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

        // Create a cancellation token source for graceful shutdown
        var cancellationSource = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) =>
        {
            Console.WriteLine("Shutdown signal received, beginning graceful shutdown");
            e.Cancel = true; // Prevent immediate termination
            cancellationSource.Cancel();
        };

        // Use the enhanced configurable middleware from McpServerExtensions
        Mcp.Net.Server.Extensions.McpServerExtensions.UseMcpServer(
            app,
            options =>
            {
                options.SsePath = "/sse";
                options.MessagesPath = "/messages";
                options.HealthCheckPath = "/health";
                options.EnableCors = true;
                // Allow all origins by default, or specify allowed origins
                // options.AllowedOrigins = new[] { "http://localhost:3000", "https://example.com" };
            }
        );

        Console.WriteLine("Health check endpoint enabled at /health");
        Console.WriteLine("SSE endpoint enabled at /sse");
        Console.WriteLine("Messages endpoint enabled at /messages");

        // Display the server URL
        // Updated to use the new type after refactoring
        var config = app.Services.GetRequiredService<Mcp.Net.Server.McpServerConfiguration>();
        Console.WriteLine($"Server started at http://{config.Hostname}:{config.Port}");
        Console.WriteLine("Press Ctrl+C to stop the server.");

        try
        {
            // Use the WebApplication.RunAsync method without the cancellation token
            var serverTask = app.RunAsync($"http://{hostname}:{port}");

            // Wait for cancellation
            await Task.Delay(Timeout.Infinite, cancellationSource.Token);

            // Stop the web application
            await app.StopAsync();
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Server shutdown complete");
        }
    }

    /// <summary>
    /// Run the server with stdio transport
    /// </summary>
    static async Task RunWithStdioTransport(CommandLineOptions options, LogLevel logLevel)
    {
        Console.WriteLine("Starting MCP server with stdio transport...");

        // Create a cancellation token source for graceful shutdown
        var cancellationSource = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) =>
        {
            Console.WriteLine("Shutdown signal received, beginning graceful shutdown");
            e.Cancel = true; // Prevent immediate termination
            cancellationSource.Cancel();
        };

        // Use the new explicit transport selection with builder pattern
        var serverBuilder = McpServerBuilder
            .ForStdio()
            .WithName(options.ServerName ?? "Simple MCP Server")
            .WithVersion("1.0.0")
            .WithInstructions("Example server with calculator and Warhammer 40k tools")
            .WithLogLevel(logLevel);

        // Add the entry assembly to scan for tools
        Assembly? entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly != null)
        {
            Console.WriteLine($"Scanning entry assembly for tools: {entryAssembly.FullName}");
        }

        // Add external tools assembly
        serverBuilder.WithAdditionalAssembly(
            typeof(Mcp.Net.Examples.ExternalTools.UtilityTools).Assembly
        );

        // Add custom tool assemblies if specified
        if (options.ToolAssemblies != null)
        {
            foreach (var assemblyPath in options.ToolAssemblies)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(assemblyPath);
                    serverBuilder.WithAdditionalAssembly(assembly);
                    Console.WriteLine($"Added tool assembly: {assembly.GetName().Name}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load assembly {assemblyPath}: {ex.Message}");
                }
            }
        }

        // Configure common log levels
        serverBuilder.ConfigureCommonLogLevels(
            toolsLevel: LogLevel.Debug,
            transportLevel: LogLevel.Debug,
            jsonRpcLevel: LogLevel.Debug
        );

        // Start the server
        var server = await serverBuilder.StartAsync();

        Console.WriteLine("Server started with stdio transport");
        Console.WriteLine("Press Ctrl+C to stop the server.");

        // Wait for cancellation
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationSource.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Server shutdown complete");
        }
    }
}
