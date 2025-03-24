using System.Text.Json.Serialization;

namespace Mcp.Net.Core.Attributes;

[AttributeUsage(AttributeTargets.Parameter)]
public class McpParameterAttribute : Attribute
{
    [JsonPropertyName("required")]
    public bool Required { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    public McpParameterAttribute(bool required = false, string description = "Default Description")
    {
        Required = required;
        Description = description;
    }
}
