namespace Mcp.Net.Examples.LLM.Models;

/// <summary>
/// Represents the result of a tool execution
/// </summary>
public class ToolCallResult
{
    public string ToolCallId { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public Dictionary<string, object> Results { get; set; } = new();
    public bool IsSuccess { get; set; } = true;
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Creates a successful tool call result
    /// </summary>
    public static ToolCallResult Success(
        string toolCallId,
        string toolName,
        Dictionary<string, object> results
    )
    {
        return new ToolCallResult
        {
            ToolCallId = toolCallId,
            ToolName = toolName,
            Results = results,
            IsSuccess = true,
        };
    }

    /// <summary>
    /// Creates a failed tool call result
    /// </summary>
    public static ToolCallResult Error(string toolCallId, string toolName, string errorMessage)
    {
        return new ToolCallResult
        {
            ToolCallId = toolCallId,
            ToolName = toolName,
            Results = new Dictionary<string, object> { { "error", errorMessage } },
            IsSuccess = false,
            ErrorMessage = errorMessage,
        };
    }
}
