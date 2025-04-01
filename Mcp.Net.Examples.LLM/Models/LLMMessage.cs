namespace Mcp.Net.Examples.LLM.Models;

public class LlmMessage
{
    /// <summary>
    /// E.g. System, User, Tool, Assistant
    /// </summary>
    public MessageType Type { get; set; }

    /// <summary>
    /// Content of the Message (from the User or LLM)
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// ID of the tool Used
    /// </summary>
    public string? ToolCallId { get; set; }

    /// <summary>
    /// Name of the Tool Used
    /// </summary>
    public string? ToolName { get; set; }

    /// <summary>
    /// Key/Value Dictionary of the ToolResults
    /// </summary>
    public Dictionary<string, object>? ToolResults { get; set; }
}