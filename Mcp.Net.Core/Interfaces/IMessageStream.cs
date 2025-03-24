using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mcp.Net.Core.Interfaces;

/// <summary>
/// Interface for streaming messages asynchronously
/// </summary>
/// <typeparam name="T">The type of messages in the stream</typeparam>
public interface IMessageStream<T>
{
    /// <summary>
    /// Reads all messages from the stream asynchronously
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An asynchronous enumerable of messages</returns>
    IAsyncEnumerable<T> ReadAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Writes a message to the stream asynchronously
    /// </summary>
    /// <param name="message">The message to write</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous write operation</returns>
    ValueTask WriteAsync(T message, CancellationToken cancellationToken);
}
