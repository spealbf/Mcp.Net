using Mcp.Net.LLM.Events;

namespace Mcp.Net.WebUi.DTOs;

/// <summary>
/// DTO for tool execution status updates
/// </summary>
public class ToolExecutionDto
{
    /// <summary>
    /// The session ID this tool execution belongs to
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// The name of the tool being executed
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// Whether the execution was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The error message, if any
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Timestamp when the execution status was updated
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Tool call arguments (input parameters)
    /// </summary>
    public Dictionary<string, object>? Arguments { get; set; }

    /// <summary>
    /// Tool call results (output)
    /// </summary>
    public Dictionary<string, object>? Results { get; set; }

    /// <summary>
    /// Create a DTO from a tool execution event args
    /// </summary>
    public static ToolExecutionDto FromEventArgs(ToolExecutionEventArgs args, string sessionId)
    {
        return new ToolExecutionDto
        {
            SessionId = sessionId,
            ToolName = args.ToolName,
            Success = args.Success,
            ErrorMessage = args.ErrorMessage,
            Timestamp = DateTime.UtcNow,
            Arguments = args.ToolCall?.Arguments,
            Results = args.ToolCall?.Results,
        };
    }
}
