using System;
using System.Threading;
using System.Threading.Tasks;
using Mcp.Net.Core.Attributes;
using Mcp.Net.Server.ServerBuilder;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.Examples.SimpleServer;

class Program
{
    static async Task Main(string[] args)
    {
        bool useHosting = args.Length > 0 && args[0] == "--host";
        bool useStdio = args.Length > 0 && args[0] == "--stdio";

        // Get log level from environment variable or use default
        var logLevelEnv = Environment.GetEnvironmentVariable("MCP_LOG_LEVEL") ?? "Debug"; // Set to Debug by default for better visibility
        if (!Enum.TryParse<LogLevel>(logLevelEnv, out var logLevel))
        {
            logLevel = LogLevel.Debug;
        }

        if (useHosting)
        {
            await RunAsHostedService(logLevel);
        }
        else
        {
            await RunStandalone(logLevel, useStdio);
        }
    }

    static async Task RunStandalone(LogLevel logLevel, bool useStdio)
    {
        // Only print to console if NOT in stdio mode
        if (!useStdio)
        {
            Console.WriteLine("Starting MCP server in standalone mode...");
        }

        var builder = new McpServerBuilder()
            .WithName("Sample MCP Server")
            .WithVersion("1.0.0")
            .WithInstructions("This is a sample MCP server")
            .UseLogLevel(logLevel)
            .UseFileLogging("./simple-server.log");

        if (useStdio)
        {
            builder.UseStdioTransport();

            var server = await builder.StartAsync();
            // No console output in stdio mode
            await WaitForCancellationAsync();
        }
        else
        {
            // Use SSE transport
            builder.UseSseTransport("http://localhost:5050");

            // When using SSE, we need to set up a web host
            var webBuilder = WebApplication.CreateBuilder();

            // Set logging to Debug level by default
            webBuilder.Logging.ClearProviders();
            webBuilder.Logging.AddConsole();
            webBuilder.Logging.SetMinimumLevel(logLevel);

            // Add services
            webBuilder.Services.AddMcpServer(b =>
            {
                b.WithName("Sample MCP Server")
                    .WithVersion("1.0.0")
                    .WithInstructions("This is a sample MCP server with calculator tools")
                    .UseLogLevel(logLevel)
                    .UseSseTransport("http://localhost:5000");
            });

            webBuilder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(builder =>
                {
                    builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                });
            });

            var app = webBuilder.Build();

            // Configure middleware
            app.UseCors();
            app.UseMcpServer();

            Console.WriteLine(
                "Server started with SSE transport at http://localhost:5000. Press Ctrl+C to stop."
            );

            // Start the server
            await app.RunAsync();
        }
    }

    static async Task RunAsHostedService(LogLevel logLevel)
    {
        Console.WriteLine("Starting MCP server as a hosted service...");

        var builder = WebApplication.CreateBuilder();

        // Add services
        builder.Services.AddMcpServer(b =>
        {
            b.WithName("Sample MCP Server")
                .WithVersion("1.0.0")
                .WithInstructions("This is a sample MCP server with calculator tools")
                .UseSseTransport("http://localhost:5000");
        });

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policyBuilder =>
            {
                policyBuilder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
            });
        });

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(logLevel);

        var app = builder.Build();

        // Configure middleware
        app.UseCors();
        app.UseMcpServer();

        Console.WriteLine(
            "Server started with SSE transport at http://localhost:5000. Press Ctrl+C to stop."
        );

        // Start the server
        await app.RunAsync();
    }

    private static Task WaitForCancellationAsync()
    {
        var tcs = new TaskCompletionSource<bool>();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            tcs.TrySetResult(true);
        };
        return tcs.Task;
    }
}

// This is needed temporarily until we add the proper reference/extension
public static class LoggingBuilderExtensions
{
    public static ILoggingBuilder AddFile(this ILoggingBuilder builder, string path)
    {
        // This would be implemented with a real file logging provider
        return builder;
    }
}
