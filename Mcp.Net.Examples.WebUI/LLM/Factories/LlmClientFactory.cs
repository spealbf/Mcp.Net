using Mcp.Net.Core.Models.Tools;
using Mcp.Net.Examples.WebUI.LLM.Clients;
using Mcp.Net.LLM.Anthropic;
using Mcp.Net.LLM.Interfaces;
using Mcp.Net.LLM.Models;
using Mcp.Net.LLM.OpenAI;

namespace Mcp.Net.Examples.WebUI.LLM.Factories;

/// <summary>
/// Factory for creating LLM clients
/// </summary>
public class LlmClientFactory
{
    private readonly ILogger<AnthropicChatClient> _anthropicLogger;
    private readonly ILogger<OpenAiChatClient> _openAiLogger;

    public LlmClientFactory(
        ILogger<AnthropicChatClient> anthropicLogger,
        ILogger<OpenAiChatClient> openAiLogger
    )
    {
        _anthropicLogger = anthropicLogger;
        _openAiLogger = openAiLogger;
    }

    /// <summary>
    /// Create a new LLM client with the specified provider and options
    /// </summary>
    public IChatClient Create(LlmProvider provider, ChatClientOptions options)
    {
        // If API key is missing, try to get from environment variables
        if (string.IsNullOrEmpty(options.ApiKey))
        {
            options.ApiKey = provider switch
            {
                LlmProvider.Anthropic => Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
                    ?? "",
                _ => Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "",
            };
        }

        // Return a stub implementation for now to allow the project to build
        return new StubChatClient(provider, options);
    }
}
