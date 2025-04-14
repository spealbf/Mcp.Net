using System.Text.Json;
using Mcp.Net.Core.Interfaces;
using Mcp.Net.Core.JsonRpc;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.Core.Transport
{
    /// <summary>
    /// Base abstract class for MCP transport implementations.
    /// Provides common functionality for both client and server transport implementations.
    /// </summary>
    public abstract class TransportBase : ITransport, IDisposable
    {
        protected readonly IMessageParser MessageParser;
        protected readonly ILogger Logger;
        protected readonly CancellationTokenSource CancellationTokenSource = new();
        protected readonly JsonSerializerOptions SerializerOptions;
        protected bool IsStarted = false;
        protected bool IsClosed = false;

        /// <inheritdoc />
        public event Action<JsonRpcRequestMessage>? OnRequest;

        /// <inheritdoc />
        public event Action<JsonRpcNotificationMessage>? OnNotification;

        /// <inheritdoc />
        public event Action<Exception>? OnError;

        /// <inheritdoc />
        public event Action? OnClose;

        /// <summary>
        /// Initializes a new instance of the <see cref="TransportBase"/> class.
        /// </summary>
        /// <param name="messageParser">Parser for JSON-RPC messages</param>
        /// <param name="logger">Logger for transport operations</param>
        protected TransportBase(IMessageParser messageParser, ILogger logger)
        {
            MessageParser = messageParser ?? throw new ArgumentNullException(nameof(messageParser));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Standard serializer options for JSON-RPC messages
            SerializerOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System
                    .Text
                    .Json
                    .Serialization
                    .JsonIgnoreCondition
                    .WhenWritingNull,
            };
        }

        /// <inheritdoc />
        public abstract Task StartAsync();

        /// <inheritdoc />
        public abstract Task SendAsync(JsonRpcResponseMessage message);

        /// <inheritdoc />
        public virtual async Task CloseAsync()
        {
            if (IsClosed)
            {
                return;
            }

            Logger.LogInformation("Closing transport");
            IsClosed = true;
            CancellationTokenSource.Cancel();

            await OnClosingAsync();

            OnClose?.Invoke();
        }

        /// <summary>
        /// Performs transport-specific cleanup when closing.
        /// Override this method in derived classes to implement transport-specific closing operations.
        /// </summary>
        protected virtual Task OnClosingAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Raises the OnRequest event with the specified request message.
        /// </summary>
        /// <param name="request">The JSON-RPC request message.</param>
        protected void RaiseOnRequest(JsonRpcRequestMessage request)
        {
            Logger.LogDebug(
                "Processing request: Method={Method}, Id={Id}",
                request.Method,
                request.Id
            );
            OnRequest?.Invoke(request);
        }

        /// <summary>
        /// Raises the OnNotification event with the specified notification message.
        /// </summary>
        /// <param name="notification">The JSON-RPC notification message.</param>
        protected void RaiseOnNotification(JsonRpcNotificationMessage notification)
        {
            Logger.LogDebug("Processing notification: Method={Method}", notification.Method);
            OnNotification?.Invoke(notification);
        }

        /// <summary>
        /// Raises the OnError event with the specified exception.
        /// </summary>
        /// <param name="exception">The exception that occurred.</param>
        protected void RaiseOnError(Exception exception)
        {
            Logger.LogError(exception, "Transport error");
            OnError?.Invoke(exception);
        }

        /// <summary>
        /// Processes a JSON-RPC message and raises the appropriate event.
        /// </summary>
        /// <param name="message">The JSON-RPC message to process.</param>
        protected void ProcessJsonRpcMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            try
            {
                if (MessageParser.IsJsonRpcRequest(message))
                {
                    var requestMessage = MessageParser.DeserializeRequest(message);

                    Logger.LogDebug(
                        "Deserialized JSON-RPC request: Method={Method}, Id={Id}",
                        requestMessage.Method,
                        requestMessage.Id
                    );

                    RaiseOnRequest(requestMessage);
                }
                else if (MessageParser.IsJsonRpcNotification(message))
                {
                    var notificationMessage = MessageParser.DeserializeNotification(message);

                    Logger.LogDebug(
                        "Deserialized JSON-RPC notification: Method={Method}",
                        notificationMessage.Method
                    );

                    RaiseOnNotification(notificationMessage);
                }
                else
                {
                    Logger.LogWarning(
                        "Received message that is neither a request nor notification: {Message}",
                        message.Length > 100 ? message.Substring(0, 97) + "..." : message
                    );
                }
            }
            catch (JsonException ex)
            {
                string truncatedMessage =
                    message.Length > 100 ? message.Substring(0, 97) + "..." : message;
                Logger.LogError(ex, "Invalid JSON message: {TruncatedMessage}", truncatedMessage);
                RaiseOnError(new Exception($"Invalid JSON message: {ex.Message}", ex));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error processing message");
                RaiseOnError(ex);
            }
        }

        /// <summary>
        /// Serializes a JSON-RPC message to a string.
        /// </summary>
        /// <param name="message">The message to serialize.</param>
        /// <returns>The serialized message.</returns>
        protected string SerializeMessage(object message)
        {
            return JsonSerializer.Serialize(message, SerializerOptions);
        }

        /// <summary>
        /// Disposes the transport resources.
        /// </summary>
        public virtual void Dispose()
        {
            if (!IsClosed)
            {
                try
                {
                    CloseAsync().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error closing transport during disposal");
                }
            }

            CancellationTokenSource.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
