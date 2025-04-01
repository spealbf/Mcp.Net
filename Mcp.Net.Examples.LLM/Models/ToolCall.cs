namespace Mcp.Net.Examples.LLM.Models;

/// <summary>
/// Represents a tool call request from an LLM
/// </summary>
public class ToolCall
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, object> Arguments { get; set; } = new();
    public Dictionary<string, object> Results { get; set; } = new();
}
