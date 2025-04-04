using Mcp.Net.LLM.Models;

namespace Mcp.Net.LLM.Interfaces;

/// <summary>
/// Interface for chat session events that UI components can subscribe to
/// </summary>
public interface IChatSessionEvents
{
    /// <summary>
    /// Fired when a chat session starts
    /// </summary>
    event EventHandler SessionStarted;

    /// <summary>
    /// Fired when a user message is received
    /// </summary>
    event EventHandler<string> UserMessageReceived;

    /// <summary>
    /// Fired when an assistant message is received
    /// </summary>
    event EventHandler<string> AssistantMessageReceived;

    /// <summary>
    /// Fired when a tool execution update occurs
    /// </summary>
    event EventHandler<ToolExecutionEventArgs> ToolExecutionUpdated;

    /// <summary>
    /// Fired when the thinking state changes
    /// </summary>
    event EventHandler<ThinkingStateEventArgs> ThinkingStateChanged;
}

/// <summary>
/// Event arguments for tool execution updates
/// </summary>
public class ToolExecutionEventArgs : EventArgs
{
    /// <summary>
    /// The name of the tool
    /// </summary>
    public string ToolName { get; }

    /// <summary>
    /// Whether the execution was successful
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// The error message, if any
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// The tool call object
    /// </summary>
    public Models.ToolCall? ToolCall { get; }

    public ToolExecutionEventArgs(string toolName, bool success, string? errorMessage = null, Models.ToolCall? toolCall = null)
    {
        ToolName = toolName;
        Success = success;
        ErrorMessage = errorMessage;
        ToolCall = toolCall;
    }
}

/// <summary>
/// Event arguments for thinking state changes
/// </summary>
public class ThinkingStateEventArgs : EventArgs
{
    /// <summary>
    /// Whether the system is in a thinking state
    /// </summary>
    public bool IsThinking { get; }

    /// <summary>
    /// The context of the thinking (e.g., "processing message", "executing tool", "processing tool results")
    /// </summary>
    public string Context { get; }

    /// <summary>
    /// The session ID that is thinking
    /// </summary>
    public string? SessionId { get; set; }

    public ThinkingStateEventArgs(bool isThinking, string context = "", string? sessionId = null)
    {
        IsThinking = isThinking;
        Context = context;
        SessionId = sessionId;
    }
}