using System;
using System.Threading.Tasks;
using Mcp.Net.Core.Interfaces;
using Mcp.Net.Core.JsonRpc;

namespace Mcp.Net.Core.Transport;

/// <summary>
/// Interface for bidirectional streaming transport operations that combines
/// both client and server capabilities
/// </summary>
public interface IStreamTransport : IClientTransport, IServerTransport
{
    // Combines capabilities of both client and server transports
    // No additional methods needed as it inherits from both interfaces
}
