using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mcp.Net.Core.Interfaces;

/// <summary>
/// Interface for writing HTTP responses, particularly for SSE
/// </summary>
public interface IResponseWriter
{
    /// <summary>
    /// Gets a value indicating whether the response is completed
    /// </summary>
    bool IsCompleted { get; }

    /// <summary>
    /// Gets the identifier for this response writer
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the remote IP address, if available
    /// </summary>
    string? RemoteIpAddress { get; }

    /// <summary>
    /// Writes content to the response
    /// </summary>
    /// <param name="content">Content to write</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous write operation</returns>
    Task WriteAsync(string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Flushes the response buffer
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous flush operation</returns>
    Task FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a header on the response
    /// </summary>
    /// <param name="name">Header name</param>
    /// <param name="value">Header value</param>
    void SetHeader(string name, string value);

    /// <summary>
    /// Gets all request headers
    /// </summary>
    /// <returns>Dictionary of request headers</returns>
    IEnumerable<KeyValuePair<string, string>> GetRequestHeaders();

    /// <summary>
    /// Completes the response
    /// </summary>
    /// <returns>A task representing the asynchronous completion operation</returns>
    Task CompleteAsync();
}
