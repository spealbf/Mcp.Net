using System;
using System.Threading;
using System.Threading.Tasks;
using Mcp.Net.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.Server;

/// <summary>
/// Implementation of <see cref="IResponseWriter"/> for HTTP responses
/// </summary>
public class HttpResponseWriter : IResponseWriter
{
    private readonly HttpResponse _response;
    private readonly ILogger<HttpResponseWriter> _logger;
    private bool _isCompleted;

    /// <inheritdoc />
    public bool IsCompleted => _isCompleted;

    /// <inheritdoc />
    public string Id { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpResponseWriter"/> class
    /// </summary>
    /// <param name="response">The HTTP response to write to</param>
    /// <param name="logger">Logger</param>
    public HttpResponseWriter(HttpResponse response, ILogger<HttpResponseWriter> logger)
    {
        _response = response ?? throw new ArgumentNullException(nameof(response));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Id = Guid.NewGuid().ToString();
    }

    /// <inheritdoc />
    public async Task WriteAsync(string content, CancellationToken cancellationToken = default)
    {
        if (_isCompleted)
        {
            _logger.LogWarning("Attempted to write to a completed response");
            return;
        }

        try
        {
            await _response.WriteAsync(content, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing to HTTP response");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if (_isCompleted)
        {
            return;
        }

        try
        {
            await _response.Body.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing HTTP response");
            throw;
        }
    }

    /// <inheritdoc />
    public void SetHeader(string name, string value)
    {
        if (_isCompleted)
        {
            _logger.LogWarning("Attempted to set header on a completed response");
            return;
        }

        _response.Headers[name] = value;
    }

    /// <inheritdoc />
    public Task CompleteAsync()
    {
        _isCompleted = true;
        _logger.LogDebug("Response completed for ID: {Id}", Id);
        return Task.CompletedTask;
    }
}
