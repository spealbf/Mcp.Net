using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Mcp.Net.Core.Interfaces;

namespace Mcp.Net.Core.JsonRpc;

/// <summary>
/// Parser specifically designed for Server-Sent Events (SSE) messages
/// </summary>
public class SseParser
{
    private readonly IMessageParser _jsonRpcParser;
    private static readonly byte[] EventPrefix = Encoding.UTF8.GetBytes("event: ");
    private static readonly byte[] DataPrefix = Encoding.UTF8.GetBytes("data: ");
    private static readonly byte[] NewLine = Encoding.UTF8.GetBytes("\n");
    private static readonly byte[] DoubleNewLine = Encoding.UTF8.GetBytes("\n\n");

    /// <summary>
    /// Initializes a new instance of the <see cref="SseParser"/> class
    /// </summary>
    /// <param name="jsonRpcParser">Parser for JSON-RPC messages in SSE data</param>
    public SseParser(IMessageParser jsonRpcParser)
    {
        _jsonRpcParser = jsonRpcParser;
    }

    /// <summary>
    /// Processes SSE data from a pipe reader
    /// </summary>
    /// <param name="reader">The pipe reader</param>
    /// <param name="eventHandler">Handler for SSE events</param>
    /// <param name="dataHandler">Handler for SSE data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the processing operation</returns>
    public async Task ProcessSseDataAsync(
        PipeReader reader,
        Action<string> eventHandler,
        Action<string> dataHandler,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // String builder for accumulating multi-line data values
            StringBuilder dataBuilder = new StringBuilder();
            string? currentEvent = null;

            while (!cancellationToken.IsCancellationRequested)
            {
                ReadResult result = await reader.ReadAsync(cancellationToken);
                ReadOnlySequence<byte> buffer = result.Buffer;

                // Process the buffer
                SequencePosition consumed = buffer.Start;

                // Process each line in the buffer
                consumed = ProcessLines(buffer, ref dataBuilder, ref currentEvent, eventHandler, dataHandler);

                // Tell the pipe reader how much of the buffer we consumed
                reader.AdvanceTo(consumed, buffer.End);

                if (result.IsCompleted)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
    }

    /// <summary>
    /// Process lines from a buffer
    /// </summary>
    private SequencePosition ProcessLines(
        ReadOnlySequence<byte> buffer,
        ref StringBuilder dataBuilder,
        ref string? currentEvent,
        Action<string> eventHandler,
        Action<string> dataHandler)
    {
        SequencePosition consumed = buffer.Start;
        SequenceReader<byte> reader = new SequenceReader<byte>(buffer);

        while (reader.TryReadTo(out ReadOnlySequence<byte> line, (byte)'\n'))
        {
            consumed = reader.Position;

            // Skip empty lines (end of event)
            if (line.Length == 0)
            {
                if (dataBuilder.Length > 0)
                {
                    string data = dataBuilder.ToString();
                    dataBuilder.Clear();

                    // Handle the complete event
                    if (currentEvent == "endpoint")
                    {
                        eventHandler(currentEvent);
                        dataHandler(data);
                    }
                    else
                    {
                        // For data events, just pass the data
                        dataHandler(data);
                    }

                    currentEvent = null;
                }
                continue;
            }

            // Get the line as span
            ReadOnlySpan<byte> lineSpan;
            if (line.IsSingleSegment)
            {
                lineSpan = line.First.Span;
            }
            else
            {
                lineSpan = line.ToArray();
            }

            // Check for event: prefix
            if (StartsWith(lineSpan, EventPrefix))
            {
                var eventValueSpan = lineSpan.Slice(EventPrefix.Length);
                currentEvent = Encoding.UTF8.GetString(eventValueSpan.ToArray());
                continue;
            }

            // Check for data: prefix
            if (StartsWith(lineSpan, DataPrefix))
            {
                var dataValueSpan = lineSpan.Slice(DataPrefix.Length);
                string data = Encoding.UTF8.GetString(dataValueSpan.ToArray());

                if (dataBuilder.Length > 0)
                {
                    dataBuilder.AppendLine();
                }
                dataBuilder.Append(data);
            }
        }

        return consumed;
    }

    /// <summary>
    /// Helper method to check if a span starts with a specific sequence
    /// </summary>
    private static bool StartsWith(ReadOnlySpan<byte> span, byte[] value)
    {
        if (span.Length < value.Length)
            return false;

        for (int i = 0; i < value.Length; i++)
        {
            if (span[i] != value[i])
                return false;
        }

        return true;
    }
}