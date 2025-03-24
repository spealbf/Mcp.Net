using System.Text.Json.Serialization;
using Mcp.Net.Core.Models.Content;

namespace Mcp.Net.Core.Models.Messages
{
    public class ResourceReadResponse
    {
        [JsonPropertyName("contents")]
        public ResourceContent[] Contents { get; set; } = Array.Empty<ResourceContent>();
    }
}
