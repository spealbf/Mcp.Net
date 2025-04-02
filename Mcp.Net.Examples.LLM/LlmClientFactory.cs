using Mcp.Net.Examples.LLM.Anthropic;
using Mcp.Net.Examples.LLM.Interfaces;
using Mcp.Net.Examples.LLM.Models;
using Mcp.Net.Examples.LLM.OpenAI;

namespace Mcp.Net.Examples.LLM;

public static class ChatClientFactory
{
    public static IChatClient Create(LlmProvider provider, ChatClientOptions options)
    {
        return provider switch
        {
            LlmProvider.OpenAI => new OpenAiChatClient(options),
            LlmProvider.Anthropic => new AnthropicChatClient(options),
            _ => throw new System.ArgumentOutOfRangeException(nameof(provider)),
        };
    }
}
