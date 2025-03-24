using System.Text.Json;
using System.Text.Json.Serialization;
using Mcp.Net.Core.Models.Capabilities;

namespace Mcp.Net.Core.Models.Messages
{
    public class InitializeResponse
    {
        [JsonPropertyName("protocolVersion")]
        public string ProtocolVersion { get; set; } = "";

        [JsonPropertyName("capabilities")]
        public ServerCapabilities Capabilities { get; set; } = new();

        [JsonPropertyName("serverInfo")]
        public ServerInfo ServerInfo { get; set; } = new();

        [JsonPropertyName("instructions")]
        public string? Instructions { get; set; }
    }
}
