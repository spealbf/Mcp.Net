using System.Text.Json.Serialization;
using Mcp.Net.Core.Models.Prompts;

namespace Mcp.Net.Core.Models.Messages;

public class PromptsListResponse
{
    [JsonPropertyName("prompts")]
    public Prompt[] Prompts { get; set; } = Array.Empty<Prompt>();
}
