using System.Text.Json;
using System.Text.Json.Serialization;
using Mcp.Net.Core.Models.Messages;

namespace Mcp.Net.Core.Models.Tools;

/// <summary>
/// Request to call a tool by name with optional arguments
/// </summary>
public record ToolCallRequest : IMcpRequest
{
    /// <summary>
    /// Name of the tool to call
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    /// <summary>
    /// Optional arguments for the tool call
    /// </summary>
    [JsonPropertyName("arguments")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Arguments { get; init; }

    /// <summary>
    /// Gets the arguments as a JsonElement
    /// </summary>
    public JsonElement? GetArguments()
    {
        if (Arguments == null)
        {
            return null;
        }

        // Convert to JSON string and then to JsonElement
        string argumentsJson = JsonSerializer.Serialize(Arguments);
        return JsonSerializer.Deserialize<JsonElement>(argumentsJson);
    }
}