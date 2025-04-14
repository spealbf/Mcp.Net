using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using Mcp.Net.Core.Interfaces;
using Mcp.Net.Core.JsonRpc;
using Mcp.Net.Core.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mcp.Net.Server.Transport.Stdio;

/// <summary>
/// Transport implementation for standard input/output streams
/// using high-performance System.IO.Pipelines
/// </summary>
public class StdioTransport : ServerMessageTransportBase
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

                // Process each line as a message
                int position = 0;
                while (position < bufferString.Length)
                {
                    int newlineIndex = bufferString.IndexOf('\n', position);
                    if (newlineIndex == -1)
                        break;

                    string line = bufferString.Substring(position, newlineIndex - position).Trim();
                    if (!string.IsNullOrEmpty(line))
                    {
                        // Process the message using the server message transport base
                        ProcessJsonRpcMessage(line);
                    }

                    position = newlineIndex + 1;
                }

                // Tell the PipeReader how much of the buffer we consumed
                _reader.AdvanceTo(buffer.GetPosition(position));

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
    public override async Task SendAsync(JsonRpcResponseMessage message)
    {
        if (IsClosed)
        {
            throw new InvalidOperationException("Transport is closed");
        }

        try
        {
            Logger.LogDebug(
                "Sending response: ID={Id}, HasResult={HasResult}, HasError={HasError}",
                message.Id,
                message.Result != null,
                message.Error != null
            );

            string json = SerializeMessage(message);
            byte[] data = Encoding.UTF8.GetBytes(json + "\n");
            await WriteRawAsync(data);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error sending message");
            RaiseOnError(ex);
            throw;
        }
    }

    /// <summary>
    /// Writes raw data to the output pipe
    /// </summary>
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