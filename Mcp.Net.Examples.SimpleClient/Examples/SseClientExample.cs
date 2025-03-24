using System;
using System.Threading.Tasks;
using Mcp.Net.Client;
using Mcp.Net.Client.Interfaces;
using Mcp.Net.Core.Models.Content;

namespace Mcp.Net.Examples.SimpleClient.Examples;

public class SseClientExample
{
    public static async Task Run(ClientOptions options)
    {
        if (string.IsNullOrEmpty(options.ServerUrl))
        {
            Console.WriteLine("Error: Server URL is required. Use --url parameter.");
            return;
        }

        Console.WriteLine("Running Example: SSE Client");
        Console.WriteLine($"Connecting to server at {options.ServerUrl}");

        IMcpClient? client = null;

        try
        {
            // Direct client instantiation
            if (options.ExampleType == ExampleType.Direct)
            {
                // Create an SSE client directly
                client = new SseMcpClient(options.ServerUrl, "SimpleClientExample", "1.0.0");
            }
            // Builder pattern
            else if (options.ExampleType == ExampleType.Builder)
            {
                // Create a client using the builder
                client = new McpClientBuilder()
                    .WithName("SimpleClientExample")
                    .WithVersion("1.0.0")
                    .UseSseTransport(options.ServerUrl)
                    .Build();
            }
            else
            {
                Console.WriteLine("Error: Invalid example type");
                return;
            }

            // Subscribe to events
            client.OnResponse += response => Console.WriteLine($"Received response: {response.Id}");
            client.OnError += error => Console.WriteLine($"Error: {error.Message}");
            client.OnClose += () => Console.WriteLine("Connection closed");

            // Initialize the client
            Console.WriteLine("Initializing client...");
            await client.Initialize();
            Console.WriteLine("Client initialized");

            // List available tools
            var tools = await client.ListTools();
            Console.WriteLine($"\nAvailable tools ({tools.Length}):");
            foreach (var tool in tools)
            {
                Console.WriteLine($"- {tool.Name}: {tool.Description}");
            }

            // Call a tool if available
            if (tools.Length > 0)
            {
                var toolName = tools[0].Name;
                Console.WriteLine($"\nCalling tool: {toolName}");

                var result = await client.CallTool(toolName, new { query = "AI assistant" });

                Console.WriteLine("Tool response:");

                // Check if we have any content
                if (result.Content == null || !result.Content.Any())
                {
                    Console.WriteLine("No content returned");
                    return;
                }

                // Process each content item
                foreach (var content in result.Content)
                {
                    if (content is TextContent textContent)
                    {
                        Console.WriteLine(textContent.Text);
                    }
                    else
                    {
                        // Try to serialize the content as JSON for display
                        try
                        {
                            var json = System.Text.Json.JsonSerializer.Serialize(
                                content,
                                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
                            );
                            Console.WriteLine($"Content type: {content.GetType().Name}");
                            Console.WriteLine(json);
                        }
                        catch
                        {
                            Console.WriteLine(
                                $"Received content of type: {content?.GetType().Name}"
                            );
                        }
                    }
                }
            }

            // List available resources if supported
            try
            {
                var resources = await client.ListResources();
                if (resources.Length > 0)
                {
                    Console.WriteLine($"\nAvailable resources ({resources.Length}):");
                    foreach (var resource in resources)
                    {
                        Console.WriteLine($"- {resource.Name}: {resource.Uri}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Resources not supported: {ex.Message}");
            }

            // List available prompts if supported
            try
            {
                var prompts = await client.ListPrompts();
                if (prompts.Length > 0)
                {
                    Console.WriteLine($"\nAvailable prompts ({prompts.Length}):");
                    foreach (var prompt in prompts)
                    {
                        Console.WriteLine($"- {prompt.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Prompts not supported: {ex.Message}");
            }
        }
        catch (HttpRequestException ex)
            when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            Console.WriteLine($"Error: Could not connect to the server at {options.ServerUrl}");
            Console.WriteLine($"Make sure the server is running. You can start it with:");
            Console.WriteLine($"  dotnet run --project ../Mcp.Net.Examples.SimpleServer");
            Console.WriteLine($"\nTechnical details: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            // Clean up
            if (client != null)
            {
                Console.WriteLine("Disposing client...");
                client.Dispose();
            }
        }
    }
}
