using System.Text.Json.Serialization;

namespace Mcp.Net.Core.Models.Resources;

public class Resource
{
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }
}
