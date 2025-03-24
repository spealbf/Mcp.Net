using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mcp.Net.Core.Models.Capabilities
{
    public class ServerCapabilities
    {
        [JsonPropertyName("tools")]
        public object? Tools { get; set; }

        [JsonPropertyName("resources")]
        public object? Resources { get; set; }

        [JsonPropertyName("prompts")]
        public object? Prompts { get; set; }

        [JsonPropertyName("sampling")]
        public object? Sampling { get; set; }
    }
}
