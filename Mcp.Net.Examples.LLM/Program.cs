using Mcp.Net.Client;
using Mcp.Net.Client.Interfaces;
using Mcp.Net.Examples.LLM.Models;

namespace Mcp.Net.Examples.LLM;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Initialize MCP client
        var mcpClient = await ConnectToMcpServer();
        var toolRegistry = await LoadMcpTools(mcpClient);

        // Determine which LLM provider to use
        var provider = DetermineProvider(args);

        // Get appropriate API key based on provider
        string? apiKey = GetApiKey(provider);
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine(
                $"Please set the {GetApiKeyEnvVarName(provider)} environment variable"
            );
            return;
        }

        // Get appropriate model name based on provider
        string modelName = GetDefaultModel(provider);

        // Create LLM chat client (through the factory to make it provider-agnostic)
        var llmClient = ChatClientFactory.Create(
            provider,
            new ChatClientOptions { ApiKey = apiKey, Model = modelName }
        );

        // Register tools with the LLM client
        llmClient.RegisterTools(toolRegistry.AllTools);

        // Create chat session and start conversation
        var chatSession = new ChatSession(llmClient, mcpClient, toolRegistry);
        await chatSession.Start();
    }

    private static LlmProvider DetermineProvider(string[] args)
    {
        // Check if provider is specified in command line arguments
        var providerArg = args.FirstOrDefault(a => a.StartsWith("--provider="));
        if (providerArg != null)
        {
            var providerName = providerArg.Split('=')[1].ToLower();
            if (providerName == "openai")
                return LlmProvider.OpenAI;
        }

        // Check if there's an environment variable setting the provider
        var providerEnv = Environment.GetEnvironmentVariable("LLM_PROVIDER")?.ToLower();
        if (providerEnv == "openai")
            return LlmProvider.OpenAI;

        // Default to Anthropic if not specified
        return LlmProvider.Anthropic;
    }

    private static string? GetApiKey(LlmProvider provider)
    {
        return provider switch
        {
            LlmProvider.Anthropic => Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"),
            _ => Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
        };
    }

    private static string GetApiKeyEnvVarName(LlmProvider provider)
    {
        return provider switch
        {
            LlmProvider.Anthropic => "ANTHROPIC_API_KEY",
            _ => "OPENAI_API_KEY",
        };
    }

    private static string GetDefaultModel(LlmProvider provider)
    {
        return provider switch
        {
            LlmProvider.Anthropic => "claude-3-5-sonnet-20240620",
            _ => "gpt-4o",
        };
    }

    private static async Task<IMcpClient> ConnectToMcpServer()
    {
        Console.WriteLine("Starting MCP LLM Function Calling Demo");

        var mcpClient = await new McpClientBuilder()
            .UseSseTransport("http://localhost:5000/")
            .WithApiKey("test-key-123")
            .BuildAndInitializeAsync();

        Console.WriteLine("Connected to MCP server");
        return mcpClient;
    }

    private static async Task<ToolRegistry> LoadMcpTools(IMcpClient mcpClient)
    {
        var tools = await mcpClient.ListTools();
        Console.WriteLine($"Found {tools.Length} tools on the server");

        var registry = new ToolRegistry();
        registry.RegisterTools(tools);

        return registry;
    }
}
