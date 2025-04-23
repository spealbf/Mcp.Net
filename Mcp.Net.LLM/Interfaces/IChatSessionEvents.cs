using Mcp.Net.LLM.Events;

namespace Mcp.Net.LLM.Interfaces;

/// <summary>
/// Interface for chat session events that UI components can subscribe to
/// </summary>
public interface IChatSessionEvents
{
    /// <summary>
    /// Fired when a chat session starts
    /// </summary>
    event EventHandler? SessionStarted;

    /// <summary>
    /// Fired when a user message is received
    /// </summary>
    event EventHandler<string>? UserMessageReceived;

    /// <summary>
    /// Fired when an assistant message is received
    /// </summary>
    event EventHandler<string>? AssistantMessageReceived;

    /// <summary>
    /// Fired when a tool execution update occurs
    /// </summary>
    event EventHandler<ToolExecutionEventArgs>? ToolExecutionUpdated;

    /// <summary>
    /// Fired when the thinking state changes
    /// </summary>
    event EventHandler<ThinkingStateEventArgs>? ThinkingStateChanged;
}
