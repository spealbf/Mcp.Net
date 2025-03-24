using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using Mcp.Net.Core.Interfaces;
using Mcp.Net.Core.JsonRpc;
using Mcp.Net.Server.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Transport implementation for standard input/output streams
/// using high-performance System.IO.Pipelines
/// </summary>
public class StdioTransport : ITransport
{
    private readonly PipeReader _reader;
    private readonly PipeWriter _writer;
    private readonly IMessageParser _parser;
    private readonly CancellationTokenSource _cts = new();
    private readonly ILogger<StdioTransport> _logger;
    private Task? _readTask;
    private bool _started = false;

    public event Action<JsonRpcRequestMessage>? OnRequest;
    public event Action<JsonRpcNotificationMessage>? OnNotification;
    public event Action<Exception>? OnError;
    public event Action? OnClose;

    /// <summary>
    /// Initializes a new instance of the <see cref="StdioTransport"/> class
    /// </summary>
    public StdioTransport(Stream? input = null, Stream? output = null)
    {
        var inputStream = input ?? Console.OpenStandardInput();
        var outputStream = output ?? Console.OpenStandardOutput();

        _reader = PipeReader.Create(inputStream);
        _writer = PipeWriter.Create(outputStream);
        _parser = new JsonRpcMessageParser();

        // In this constructor, use a NullLogger to avoid any logging to stdout
        _logger = NullLogger<StdioTransport>.Instance;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StdioTransport"/> class with dependency injection
    /// </summary>
    public StdioTransport(
        Stream input,
        Stream output,
        IMessageParser parser,
        ILogger<StdioTransport> logger
    )
    {
        _reader = PipeReader.Create(input);
        _writer = PipeWriter.Create(output);
        _parser = parser;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync()
    {
        if (_started)
        {
            throw new InvalidOperationException(
                "StdioServerTransport already started! If using Server class, note that connect() calls start() automatically."
            );
        }

        _started = true;
        _readTask = ProcessMessagesAsync();
        Logger.Debug("StdioServerTransport started");
        return Task.CompletedTask;
    }

    private async Task ProcessMessagesAsync()
    {
        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                ReadResult result = await _reader.ReadAsync(_cts.Token);
                ReadOnlySequence<byte> buffer = result.Buffer;

                // Convert to string for processing (we can optimize this later)
                string bufferString = Encoding.UTF8.GetString(buffer.ToArray());

                // Process the buffer using our parser
                ProcessBufferWithParser(bufferString.AsSpan(), out int bytesConsumed);

                // Tell the PipeReader how much of the buffer we consumed
                _reader.AdvanceTo(buffer.GetPosition(bytesConsumed));

                // Break the loop if there's no more data coming
                if (result.IsCompleted)
                {
                    Logger.Information("End of input stream detected, closing stdio transport");
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Debug("StdioServerTransport read operation cancelled");
        }
        catch (Exception ex)
        {
            Logger.Error("Error reading from stdio", ex);
            OnError?.Invoke(ex);
        }
        finally
        {
            await _reader.CompleteAsync();
            Logger.Information("Stdio read loop terminated");
            OnClose?.Invoke();
        }
    }

    private void ProcessBufferWithParser(ReadOnlySpan<char> buffer, out int bytesConsumed)
    {
        bytesConsumed = 0;
        int position = 0;

        while (position < buffer.Length)
        {
            ReadOnlySpan<char> remaining = buffer.Slice(position);

            // Try to parse a message from the buffer
            if (_parser.TryParseMessage(remaining, out string message, out int consumed))
            {
                try
                {
                    // Skip empty messages
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        // Determine if this is a request or notification
                        if (_parser.IsJsonRpcRequest(message))
                        {
                            var requestMessage = _parser.DeserializeRequest(message);
                            Logger.Debug(
                                "Deserialized JSON-RPC request: Method={Method}, Id={Id}",
                                requestMessage.Method,
                                requestMessage.Id
                            );
                            OnRequest?.Invoke(requestMessage);
                        }
                        else if (_parser.IsJsonRpcNotification(message))
                        {
                            var notificationMessage = _parser.DeserializeNotification(message);
                            Logger.Debug(
                                "Deserialized JSON-RPC notification: Method={Method}",
                                notificationMessage.Method
                            );
                            OnNotification?.Invoke(notificationMessage);
                        }
                        else
                        {
                            Logger.Warning(
                                "Received message that is neither a request nor notification: {Message}",
                                message.Length > 100 ? message.Substring(0, 97) + "..." : message
                            );
                        }
                    }

                    // Move position forward by the characters consumed
                    position += consumed;
                    bytesConsumed = position;
                }
                catch (JsonException ex)
                {
                    // Create a truncated version of the message for logging
                    string truncatedMessage =
                        message.Length > 100 ? message.Substring(0, 97) + "..." : message;
                    Logger.Error("Invalid JSON message: {TruncatedMessage}", ex, truncatedMessage);

                    OnError?.Invoke(new Exception($"Invalid JSON message: {ex.Message}", ex));

                    // Move position forward by the characters consumed even on error
                    position += consumed;
                    bytesConsumed = position;
                }
            }
            else
            {
                // We couldn't parse a complete message, so stop and wait for more data
                break;
            }
        }
    }

    /// <inheritdoc />
    public async Task SendAsync(JsonRpcResponseMessage responseMessage)
    {
        try
        {
            // Configure serializer to ignore null values
            var options = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System
                    .Text
                    .Json
                    .Serialization
                    .JsonIgnoreCondition
                    .WhenWritingNull,
            };

            // Serialize to JSON only sending the raw JSON to stdout - no logs
            string json = JsonSerializer.Serialize(responseMessage, options);

            // Use the centralized Logger class to log to file, not console
            // NullLogger will suppress these, but we also log through the centralized Logger
            Logger.Debug("Raw JSON response being sent: {Json}", json);
            Logger.Debug(
                "Sending response: ID={Id}, HasResult={HasResult}, HasError={HasError}",
                responseMessage.Id,
                responseMessage.Result != null,
                responseMessage.Error != null
            );

            // Write just the JSON response to the pipe, nothing else
            byte[] bytes = Encoding.UTF8.GetBytes(json + "\n");
            await _writer.WriteAsync(bytes, _cts.Token);
            await _writer.FlushAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            // Use the centralized Logger class to log to file, not console
            Logger.Error("Error sending message over stdio", ex);
            OnError?.Invoke(ex);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task CloseAsync()
    {
        // Use the centralized Logger class to log to file, not console
        Logger.Information("Closing stdio transport");
        _cts.Cancel();

        // Complete writer and reader
        await _writer.CompleteAsync();

        OnClose?.Invoke();
        return;
    }
}
