using System.Threading.Tasks;
using Mcp.Net.Client;
using Mcp.Net.Client.Interfaces;
using Mcp.Net.Core.Models.Content;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.Examples.SimpleClient.Examples;

public class DependencyInjectionExample
{
    public static async Task Run(ClientOptions options)
    {
        Console.WriteLine("Running Example: Dependency Injection");

        try
        {
            // Create and configure the host
            var host = Host.CreateDefaultBuilder()
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                })
                .ConfigureServices(services =>
                {
                    // Register the MCP client
                    if (!string.IsNullOrEmpty(options.ServerUrl))
                    {
                        Console.WriteLine($"Registering SSE client for {options.ServerUrl}");
                        services.AddMcpClient(client =>
                        {
                            client.UseSseTransport(options.ServerUrl)
                                .WithName("SimpleClientExample")
                                .WithVersion("1.0.0");
                        });
                    }
                    else if (!string.IsNullOrEmpty(options.ServerCommand))
                    {
                        Console.WriteLine($"Registering Stdio client for command: {options.ServerCommand}");
                        services.AddMcpClient(client =>
                        {
                            client.UseStdioTransport(options.ServerCommand)
                                .WithName("SimpleClientExample")
                                .WithVersion("1.0.0");
                        });
                    }
                    else
                    {
                        throw new ArgumentException("Either --url or --command must be specified");
                    }

                    // Register our example service
                    services.AddHostedService<ExampleClientService>();
                })
                .Build();

            // Run the host
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}

public class ExampleClientService : IHostedService
{
    private readonly IMcpClient _client;
    private readonly ILogger<ExampleClientService> _logger;

    public ExampleClientService(IMcpClient client, ILogger<ExampleClientService> logger)
    {
        _client = client;
        _logger = logger;

        // Subscribe to events
        _client.OnResponse += response => _logger.LogInformation("Received response: {ResponseId}", response.Id);
        _client.OnError += error => _logger.LogError(error, "Client error: {ErrorMessage}", error.Message);
        _client.OnClose += () => _logger.LogInformation("Connection closed");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ExampleClientService starting");

        try
        {
            // Initialize the client
            await _client.Initialize();

            // List available tools
            var tools = await _client.ListTools();
            _logger.LogInformation("Available tools ({ToolCount}):", tools.Length);
            foreach (var tool in tools)
            {
                _logger.LogInformation("- {ToolName}: {ToolDescription}", tool.Name, tool.Description);
            }

            // Call a tool if available
            if (tools.Length > 0)
            {
                var toolName = tools[0].Name;
                _logger.LogInformation("Calling tool: {ToolName}", toolName);
                
                var result = await _client.CallTool(toolName, new { query = "AI assistant" });
                
                if (result.Content is TextContent textContent)
                {
                    _logger.LogInformation("Tool response: {ToolResponse}", textContent.Text);
                }
                else
                {
                    _logger.LogInformation("Received non-text content: {ContentType}", 
                        result.Content?.GetType().Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ExampleClientService: {ErrorMessage}", ex.Message);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ExampleClientService stopping");
        _client.Dispose();
        return Task.CompletedTask;
    }
}