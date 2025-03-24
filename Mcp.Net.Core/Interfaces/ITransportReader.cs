using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace Mcp.Net.Core.Interfaces;

/// <summary>
/// Interface for reading from a transport
/// </summary>
public interface ITransportReader
{
    /// <summary>
    /// Reads data asynchronously from the transport
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The read result containing the buffer and completion status</returns>
    Task<ReadResult> ReadAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Advances the reader to the consumed position
    /// </summary>
    /// <param name="consumed">The position that was consumed</param>
    void AdvanceTo(SequencePosition consumed);
}
