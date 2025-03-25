using System.Text.Json.Serialization;

namespace Mcp.Net.Core.Models.Capabilities;

/// <summary>
/// Options for the MCP server
/// </summary>
/// <remarks>
/// Contains server configuration options and capabilities.
/// </remarks>
public class ServerOptions
{
    /// <summary>
    /// The server capabilities
    /// </summary>
    [JsonPropertyName("capabilities")]
    public ServerCapabilities? Capabilities { get; set; }

    /// <summary>
    /// Instructions for using the server
    /// </summary>
    [JsonPropertyName("instructions")]
    public string? Instructions { get; set; }
}

/// <summary>
/// Common configuration options for MCP servers
/// </summary>
public class McpServerConfiguration
{
    /// <summary>
    /// The port number to use for the SSE transport
    /// </summary>
    /// <remarks>Default is 5000</remarks>
    public int Port { get; set; } = 5000;

    /// <summary>
    /// The hostname to bind to
    /// </summary>
    /// <remarks>Default is "localhost"</remarks>
    public string Hostname { get; set; } = "localhost";

    /// <summary>
    /// Gets the complete base URL including protocol, hostname and port
    /// </summary>
    public string BaseUrl => $"http://{Hostname}:{Port}";
}
