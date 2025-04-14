using Mcp.Net.Core.Transport;

namespace Mcp.Net.Server.ServerBuilder;

/// <summary>
/// Interface for transport builders that can create server transports.
/// </summary>
public interface ITransportBuilder
{
    /// <summary>
    /// Builds a server transport with the configured settings.
    /// </summary>
    /// <returns>The configured server transport instance</returns>
    IServerTransport BuildTransport();
}