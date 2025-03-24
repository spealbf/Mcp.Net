using System.Text.Json;
using System.Text.Json.Serialization;
using Mcp.Net.Core.Models.Capabilities;

namespace Mcp.Net.Core.Models.Messages;

/// <summary>
/// Base interface for all MCP request types
/// </summary>
public interface IMcpRequest { }

/// <summary>
/// Initialize request parameters
/// </summary>
public record InitializeRequest : IMcpRequest
{
    /// <summary>
    /// The protocol version being used
    /// </summary>
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; init; } = "";

    /// <summary>
    /// Information about the client
    /// </summary>
    [JsonPropertyName("clientInfo")]
    public ClientInfo? ClientInfo { get; init; }

    /// <summary>
    /// Client capabilities
    /// </summary>
    [JsonPropertyName("capabilities")]
    public ClientCapabilities? Capabilities { get; init; }
}

/// <summary>
/// Request to list available tools
/// </summary>
public record ListToolsRequest : IMcpRequest
{
    // Empty request record
}

/// <summary>
/// Request to list available resources
/// </summary>
public record ResourcesListRequest : IMcpRequest
{
    // Empty request record
}

/// <summary>
/// Request to read a specific resource
/// </summary>
public record ResourcesReadRequest : IMcpRequest
{
    /// <summary>
    /// URI of the resource to read
    /// </summary>
    [JsonPropertyName("uri")]
    public string Uri { get; init; } = "";
}

/// <summary>
/// Request to list available prompts
/// </summary>
public record PromptsListRequest : IMcpRequest
{
    // Empty request record
}

/// <summary>
/// Request to get a specific prompt
/// </summary>
public record PromptsGetRequest : IMcpRequest
{
    /// <summary>
    /// Name of the prompt to get
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";
}
