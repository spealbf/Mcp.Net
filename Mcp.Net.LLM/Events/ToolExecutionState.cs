namespace Mcp.Net.LLM.Events;

/// <summary>
/// Represents the current state of a tool execution
/// </summary>
public enum ToolExecutionState
{
    /// <summary>
    /// The execution state is unknown
    /// </summary>
    Unknown,

    /// <summary>
    /// The tool execution is starting
    /// </summary>
    Starting,

    /// <summary>
    /// The tool execution is in progress
    /// </summary>
    InProgress,

    /// <summary>
    /// The tool execution has been completed
    /// </summary>
    Completed,

    /// <summary>
    /// The tool execution has failed
    /// </summary>
    Failed,
}