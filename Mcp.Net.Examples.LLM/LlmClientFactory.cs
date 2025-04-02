using Mcp.Net.Examples.LLM.Anthropic;
using Mcp.Net.Examples.LLM.Interfaces;
using Mcp.Net.Examples.LLM.Models;
using Mcp.Net.Examples.LLM.OpenAI;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.Examples.LLM;

public static class ChatClientFactory
{
    public static IChatClient Create(
        LlmProvider provider,
        ChatClientOptions options,
        ILogger<OpenAiChatClient> openAiLogger,
        ILogger<AnthropicChatClient> anthropicLogger
    )
    {
        return provider switch
        {
            LlmProvider.OpenAI => new OpenAiChatClient(options, openAiLogger),
            LlmProvider.Anthropic => new AnthropicChatClient(options, anthropicLogger),
            _ => throw new System.ArgumentOutOfRangeException(nameof(provider)),
        };
    }
}
