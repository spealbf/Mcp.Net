using System.Text.Json.Serialization;

namespace Mcp.Net.Core.Models.Prompts;

public class PromptArgument
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("required")]
    public bool Required { get; set; }
}
