using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mcp.Net.Core.Models.Tools
{
    public class Tool
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("inputSchema")]
        public JsonElement InputSchema { get; set; }
    }
}

