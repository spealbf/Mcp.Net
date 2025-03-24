using System.Text.Json.Serialization;
using Mcp.Net.Core.Models.Resources;

namespace Mcp.Net.Core.Models.Messages;

public class ResourcesListResponse
{
    [JsonPropertyName("resources")]
    public Resource[] Resources { get; set; } = Array.Empty<Resource>();
}
