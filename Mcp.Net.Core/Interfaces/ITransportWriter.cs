using System;
using System.Threading;
using System.Threading.Tasks;

namespace Mcp.Net.Core.Interfaces;

/// <summary>
/// Interface for writing to a transport
/// </summary>
public interface ITransportWriter
{
    /// <summary>
    /// Writes data asynchronously to the transport
    /// </summary>
    /// <param name="buffer">Buffer containing data to write</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous write operation</returns>
    ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken);

    /// <summary>
    /// Flushes pending data to the transport
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous flush operation</returns>
    ValueTask FlushAsync(CancellationToken cancellationToken);
}
