using Mcp.Net.Examples.WebUI.Adapters.Interfaces;
using Mcp.Net.LLM.Models;

namespace Mcp.Net.Examples.WebUI.Chat.Interfaces;

/// <summary>
/// Factory interface for creating chat session components
/// </summary>
public interface IChatFactory
{
    /// <summary>
    /// Create a new SignalR chat adapter
    /// </summary>
    /// <param name="sessionId">Unique identifier for the chat session</param>
    /// <param name="model">LLM model to use</param>
    /// <param name="provider">LLM provider to use</param>
    /// <param name="systemPrompt">Optional system prompt</param>
    /// <returns>A configured SignalR chat adapter</returns>
    ISignalRChatAdapter CreateSignalRAdapter(
        string sessionId, 
        string? model = null,
        string? provider = null,
        string? systemPrompt = null
    );
    
    /// <summary>
    /// Create session metadata for a new chat
    /// </summary>
    /// <param name="sessionId">Unique identifier for the chat session</param>
    /// <param name="model">LLM model to use</param>
    /// <param name="provider">LLM provider to use</param>
    /// <param name="systemPrompt">Optional system prompt</param>
    /// <returns>Configured ChatSessionMetadata</returns>
    ChatSessionMetadata CreateSessionMetadata(
        string sessionId,
        string? model = null,
        string? provider = null,
        string? systemPrompt = null
    );
}