using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mcp.Net.Core.Models.Content
{
    public class ResourceContent
    {
        [JsonPropertyName("uri")]
        public string Uri { get; set; } = "";

        [JsonPropertyName("mimeType")]
        public string? MimeType { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("blob")]
        public string? Blob { get; set; }
    }
}
