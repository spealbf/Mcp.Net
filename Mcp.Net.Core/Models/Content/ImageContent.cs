using System.Text.Json;
using System.Text.Json.Serialization;
using Mcp.Net.Core.Models.Content;

namespace Mcp.Net.Core.Models.Content
{
    public class ImageContent : ContentBase
    {
        [JsonPropertyName("type")]
        public override string Type => "image";

        [JsonPropertyName("data")]
        public string Data { get; set; } = "";

        [JsonPropertyName("mimeType")]
        public string MimeType { get; set; } = "";
    }
}
