using System.Text.Json;
using System.Text.Json.Serialization;
using Mcp.Net.Core.Models.Content;

namespace Mcp.Net.Core.Models.Content
{
    public class TextContent : ContentBase
    {
        [JsonPropertyName("type")]
        public override string Type => "text";

        [JsonPropertyName("text")]
        public string Text { get; set; } = "";
    }
}
