using Mcp.Net.Core.Transport;

namespace Mcp.Net.Server.ServerBuilder;

/// <summary>
/// Core interface for MCP server builders. Provides methods to build and start a server.
/// </summary>
public interface IMcpServerBuilder
{
    /// <summary>
    /// Builds a server with the configured settings but doesn't connect to transport.
    /// </summary>
    /// <returns>The configured McpServer instance</returns>
    McpServer Build();

    /// <summary>
    /// Starts a server asynchronously by connecting it to a transport.
    /// </summary>
    /// <param name="server">The server to start</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task StartAsync(McpServer server);
}