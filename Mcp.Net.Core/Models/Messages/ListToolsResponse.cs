using System.Text.Json;
using System.Text.Json.Serialization;
using Mcp.Net.Core.Models.Tools;

namespace Mcp.Net.Core.Models.Messages
{
    public class ListToolsResponse
    {
        [JsonPropertyName("tools")]
        public Tool[] Tools { get; set; } = Array.Empty<Tool>();
    }
}
