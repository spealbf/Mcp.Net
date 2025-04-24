using Mcp.Net.LLM.Interfaces;
using Mcp.Net.LLM.Models;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.LLM.Factories;

/// <summary>
/// Factory for creating LLM chat clients
/// </summary>
public class ChatClientFactory : IChatClientFactory
{
    private readonly ILogger<OpenAI.OpenAiChatClient> _openAiLogger;
    private readonly ILogger<Anthropic.AnthropicChatClient> _anthropicLogger;

    public ChatClientFactory(
        ILogger<OpenAI.OpenAiChatClient> openAiLogger,
        ILogger<Anthropic.AnthropicChatClient> anthropicLogger
    )
    {
        _openAiLogger = openAiLogger;
        _anthropicLogger = anthropicLogger;
    }

    /// <summary>
    /// Creates an LLM chat client for the specified provider
    /// </summary>
    public IChatClient Create(LlmProvider provider, ChatClientOptions options)
    {
        return provider switch
        {
            LlmProvider.OpenAI => new OpenAI.OpenAiChatClient(options, _openAiLogger),
            LlmProvider.Anthropic => new Anthropic.AnthropicChatClient(options, _anthropicLogger),
            _ => throw new System.ArgumentOutOfRangeException(nameof(provider)),
        };
    }
}