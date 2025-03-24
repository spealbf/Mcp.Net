using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mcp.Net.Core.Models.Capabilities
{
    public class ServerOptions
    {
        public ServerCapabilities? Capabilities { get; set; }
        public string? Instructions { get; set; }
    }
}
