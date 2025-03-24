using System.Text.Json.Serialization;

namespace Mcp.Net.Core.Models.Prompts;

public class Prompt
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("arguments")]
    public PromptArgument[]? Arguments { get; set; }
}
