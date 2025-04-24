using Mcp.Net.LLM.Interfaces;
using Mcp.Net.LLM.Models;

public interface IChatClientFactory
{
    IChatClient Create(LlmProvider provider, ChatClientOptions options);
}
