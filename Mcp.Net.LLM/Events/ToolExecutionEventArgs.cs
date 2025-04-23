using Mcp.Net.LLM.Models;

namespace Mcp.Net.LLM.Events;

/// <summary>
/// Event arguments for tool execution updates
/// </summary>
public class ToolExecutionEventArgs : EventArgs
{
    /// <summary>
    /// The name of the tool being executed
    /// </summary>
    public string ToolName { get; }

    /// <summary>
    /// Whether the tool execution was successful
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Error message, if the tool execution failed
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// The tool call being executed
    /// </summary>
    public ToolCall? ToolCall { get; }

    /// <summary>
    /// The current state of the tool execution
    /// </summary>
    public ToolExecutionState ExecutionState { get; }

    public ToolExecutionEventArgs(
        string toolName,
        bool success,
        string? errorMessage = null,
        ToolCall? toolCall = null,
        ToolExecutionState executionState = ToolExecutionState.Unknown
    )
    {
        ToolName = toolName;
        Success = success;
        ErrorMessage = errorMessage;
        ToolCall = toolCall;
        ExecutionState = executionState;
    }
}