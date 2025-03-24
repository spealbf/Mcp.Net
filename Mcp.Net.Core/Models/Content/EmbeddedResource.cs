using System.Text.Json;
using System.Text.Json.Serialization;
using Mcp.Net.Core.Models.Content;

namespace Mcp.Net.Core.Models.Content
{
    public class EmbeddedResource : ContentBase
    {
        [JsonPropertyName("type")]
        public override string Type => "resource";

        [JsonPropertyName("resource")]
        public ResourceContent Resource { get; set; } = new();
    }
}
