using System.Text.Json.Serialization;

namespace Mcp.Net.Core.Models.Messages;

public class PromptsGetResponse
{
    [JsonPropertyName("messages")]
    public object[] Messages { get; set; } = Array.Empty<object>();
}
