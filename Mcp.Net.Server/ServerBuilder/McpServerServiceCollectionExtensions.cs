using System;
using System.Threading.Tasks;
using Mcp.Net.Core.Interfaces;
using Mcp.Net.Core.JsonRpc;
using Mcp.Net.Core.Models.Capabilities;
using Mcp.Net.Server.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.Server.ServerBuilder;

/// <summary>
/// Extension methods for adding MCP server capabilities to an application
/// </summary>
public static class McpServerServiceCollectionExtensions
{
    /// <summary>
    /// Adds an MCP server to the application builder
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <returns>The application builder for chaining</returns>
    public static IApplicationBuilder UseMcpServer(this IApplicationBuilder app)
    {
        var connectionManager = app.ApplicationServices.GetRequiredService<SseConnectionManager>();
        var server = app.ApplicationServices.GetRequiredService<McpServer>();
        var transportFactory = app.ApplicationServices.GetRequiredService<ISseTransportFactory>();

        app.Map(
            "/sse",
            sseApp =>
            {
                sseApp.Run(async context =>
                {
                    var transport = transportFactory.CreateTransport(context.Response);

                    try
                    {
                        await server.ConnectAsync(transport);

                        // Keep the connection alive until client disconnects
                        await Task.Delay(-1, context.RequestAborted);
                    }
                    catch (TaskCanceledException)
                    {
                        // Connection closed normally
                    }
                    catch (Exception ex)
                    {
                        // Log error using the proper logger
                        // Use scoped logger
                        var scopedLogger = app
                            .ApplicationServices.GetRequiredService<ILoggerFactory>()
                            .CreateLogger("SseEndpoint");
                        scopedLogger.LogError(ex, "Error in SSE connection");
                    }
                    finally
                    {
                        await transport.CloseAsync();
                    }
                });
            }
        );

        app.Map(
            "/messages",
            messagesApp =>
            {
                messagesApp.Run(async context =>
                {
                    var sessionId = context.Request.Query["sessionId"].ToString();
                    if (string.IsNullOrEmpty(sessionId))
                    {
                        context.Response.StatusCode = 400;
                        await context.Response.WriteAsJsonAsync(
                            new { error = "Missing sessionId" }
                        );
                        return;
                    }

                    var transport = connectionManager.GetTransport(sessionId);
                    if (transport == null)
                    {
                        context.Response.StatusCode = 404;
                        await context.Response.WriteAsJsonAsync(
                            new { error = "Session not found" }
                        );
                        return;
                    }

                    try
                    {
                        // Since clients only send requests to servers, treat all messages as requests
                        var request =
                            await System.Text.Json.JsonSerializer.DeserializeAsync<Core.JsonRpc.JsonRpcRequestMessage>(
                                context.Request.Body
                            );

                        if (request == null)
                        {
                            context.Response.StatusCode = 400;
                            await context.Response.WriteAsJsonAsync(
                                new { error = "Invalid request format" }
                            );
                            return;
                        }

                        transport.HandlePostMessage(request);

                        context.Response.StatusCode = 202;
                        await context.Response.WriteAsJsonAsync(new { status = "accepted" });
                    }
                    catch (Exception ex)
                    {
                        context.Response.StatusCode = 500;
                        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
                    }
                });
            }
        );

        return app;
    }

    /// <summary>
    /// Adds MCP server services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Builder configuration delegate</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddMcpServer(
        this IServiceCollection services,
        Action<McpServerBuilder> configure
    )
    {
        var builder = new McpServerBuilder();
        configure(builder);

        // Register logging services
        services.AddSingleton<IMcpLoggerConfiguration>(McpLoggerConfiguration.Instance);
        services.AddSingleton<ILoggerFactory>(sp =>
            McpLoggerConfiguration.Instance.CreateLoggerFactory()
        );

        // Register server as singleton
        services.AddSingleton(sp => builder.Build());

        if (builder.IsUsingSse)
        {
            // Register SSE-specific services
            services.AddSingleton<SseConnectionManager>();
            services.AddSingleton<ISseTransportFactory, SseTransportFactory>();

            // Register server configuration
            services.AddSingleton(
                new McpServerConfiguration { Port = builder.Port, Hostname = builder.Hostname }
            );

            // No direct transport registration for SSE
            // Transports will be created per connection
        }
        else
        {
            // Register standard stdio transport for hosted service
            services.AddSingleton<ITransport>(sp => new StdioTransport());
        }

        // Register hosted service
        services.AddHostedService<McpServerHostedService>();

        return services;
    }
}
