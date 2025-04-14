using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using Mcp.Net.Core.Interfaces;
using Mcp.Net.Core.JsonRpc;
using Mcp.Net.Core.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mcp.Net.Server;

/// <summary>
/// Transport implementation for standard input/output streams
/// using high-performance System.IO.Pipelines
/// </summary>
public class StdioTransport : MessageTransportBase
{
    private readonly PipeReader _reader;
    private readonly PipeWriter _writer;
    private Task? _readTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="StdioTransport"/> class
    /// </summary>
    public StdioTransport(Stream? input = null, Stream? output = null)
        : base(new JsonRpcMessageParser(), NullLogger<StdioTransport>.Instance)
    {
        var inputStream = input ?? Console.OpenStandardInput();
        var outputStream = output ?? Console.OpenStandardOutput();

        _reader = PipeReader.Create(inputStream);
        _writer = PipeWriter.Create(outputStream);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StdioTransport"/> class with a logger
    /// </summary>
    public StdioTransport(Stream input, Stream output, ILogger<StdioTransport> logger)
        : base(new JsonRpcMessageParser(), logger)
    {
        _reader = PipeReader.Create(input);
        _writer = PipeWriter.Create(output);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StdioTransport"/> class with full dependency injection
    /// </summary>
    public StdioTransport(
        Stream input,
        Stream output,
        IMessageParser parser,
        ILogger<StdioTransport> logger
    ) : base(parser, logger)
    {
        _reader = PipeReader.Create(input);
        _writer = PipeWriter.Create(output);
    }

    /// <inheritdoc />
    public override Task StartAsync()
    {
        if (IsStarted)
        {
            throw new InvalidOperationException(
                "StdioTransport already started! If using Server class, note that connect() calls start() automatically."
            );
        }

        IsStarted = true;
        _readTask = ProcessMessagesAsync();
        Logger.LogDebug("StdioTransport started");
        return Task.CompletedTask;
    }

    private async Task ProcessMessagesAsync()
    {
        try
        {
            while (!CancellationTokenSource.IsCancellationRequested)
            {
                ReadResult result = await _reader.ReadAsync(CancellationTokenSource.Token);
                ReadOnlySequence<byte> buffer = result.Buffer;

                // Convert to string for processing
                string bufferString = Encoding.UTF8.GetString(buffer.ToArray());

                // Process the buffer using our parser
                ProcessBuffer(bufferString.AsSpan(), out int bytesConsumed);

                // Tell the PipeReader how much of the buffer we consumed
                _reader.AdvanceTo(buffer.GetPosition(bytesConsumed));

                // Break the loop if there's no more data coming
                if (result.IsCompleted)
                {
                    Logger.LogInformation("End of input stream detected, closing stdio transport");
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug("StdioTransport read operation cancelled");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error reading from stdio");
            RaiseOnError(ex);
        }
        finally
        {
            await _reader.CompleteAsync();
            Logger.LogInformation("Stdio read loop terminated");
        }
    }

    /// <inheritdoc />
    protected override async Task WriteRawAsync(byte[] data)
    {
        await _writer.WriteAsync(data, CancellationTokenSource.Token);
        await _writer.FlushAsync(CancellationTokenSource.Token);
    }

    /// <inheritdoc />
    protected override async Task OnClosingAsync()
    {
        await _writer.CompleteAsync();
        await base.OnClosingAsync();
    }
}