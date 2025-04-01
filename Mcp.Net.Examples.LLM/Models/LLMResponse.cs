namespace Mcp.Net.Examples.LLM.Models;

public class LlmResponse
{
    public string Text { get; set; } = string.Empty;
    public List<ToolCall> ToolCalls { get; set; } = new();
    public bool RequiresToolExecution => ToolCalls.Count > 0;
    public MessageType MessageType { get; set; }
}
