using Mcp.Net.Server.Options;

namespace Mcp.Net.Server;

/// <summary>
/// Simple configuration object for the MCP server.
/// </summary>
/// <remarks>
/// This class serves as a simple data container for server configuration settings.
/// For more advanced configuration scenarios, use the <see cref="ServerConfiguration"/> class.
/// </remarks>
public class McpServerConfiguration
{
    /// <summary>
    /// Gets or sets the hostname to listen on.
    /// </summary>
    public string Hostname { get; set; } = "localhost";
    
    /// <summary>
    /// Gets or sets the port to listen on.
    /// </summary>
    public int Port { get; set; } = 5000;
    
    /// <summary>
    /// Gets or sets the URL scheme (http/https).
    /// </summary>
    public string Scheme { get; set; } = "http";
    
    /// <summary>
    /// Gets the server URL based on the configured hostname, port, and scheme.
    /// </summary>
    public string ServerUrl => $"{Scheme}://{Hostname}:{Port}";
    
    /// <summary>
    /// Converts this instance to a <see cref="SseServerOptions"/> object.
    /// </summary>
    /// <returns>A new instance of <see cref="SseServerOptions"/> with values copied from this instance.</returns>
    public SseServerOptions ToSseServerOptions()
    {
        return new SseServerOptions
        {
            Hostname = Hostname,
            Port = Port,
            Scheme = Scheme
        };
    }
    
    /// <summary>
    /// Creates a new instance of <see cref="McpServerConfiguration"/> from the specified <see cref="SseServerOptions"/>.
    /// </summary>
    /// <param name="options">The options to copy values from.</param>
    /// <returns>A new instance of <see cref="McpServerConfiguration"/>.</returns>
    public static McpServerConfiguration FromSseServerOptions(SseServerOptions options)
    {
        return new McpServerConfiguration
        {
            Hostname = options.Hostname,
            Port = options.Port,
            Scheme = options.Scheme
        };
    }
}