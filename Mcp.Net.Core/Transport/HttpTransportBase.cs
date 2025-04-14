using System;
using System.Threading.Tasks;
using Mcp.Net.Core.Interfaces;
using Mcp.Net.Core.JsonRpc;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.Core.Transport
{
    /// <summary>
    /// Base class for HTTP-based server transports like SSE
    /// </summary>
    public abstract class HttpTransportBase : ServerTransportBase
    {
        protected readonly IResponseWriter ResponseWriter;

        /// <summary>
        /// Gets the unique identifier for this transport session
        /// </summary>
        public string SessionId => ResponseWriter.Id;

        /// <summary>
        /// Gets or sets the metadata dictionary for this transport
        /// </summary>
        public Dictionary<string, string> Metadata { get; } = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpTransportBase"/> class
        /// </summary>
        /// <param name="responseWriter">Response writer for the HTTP transport</param>
        /// <param name="messageParser">Parser for JSON-RPC messages</param>
        /// <param name="logger">Logger for transport operations</param>
        protected HttpTransportBase(
            IResponseWriter responseWriter,
            IMessageParser messageParser,
            ILogger logger
        )
            : base(messageParser, logger)
        {
            ResponseWriter = responseWriter ?? throw new ArgumentNullException(nameof(responseWriter));
        }

        /// <summary>
        /// Handles an HTTP-based JSON-RPC request message
        /// </summary>
        /// <param name="requestMessage">The JSON-RPC request message</param>
        public void HandleRequest(JsonRpcRequestMessage requestMessage)
        {
            if (IsClosed)
            {
                throw new InvalidOperationException("Transport is closed");
            }

            if (requestMessage != null && !string.IsNullOrEmpty(requestMessage.Method))
            {
                Logger.LogDebug(
                    "Handling request: Method={Method}, Id={RequestId}",
                    requestMessage.Method,
                    requestMessage.Id
                );

                RaiseOnRequest(requestMessage);
            }
            else
            {
                Logger.LogWarning("Received invalid request with missing method or null request");
            }
        }

        /// <summary>
        /// Handles an HTTP-based JSON-RPC notification message
        /// </summary>
        /// <param name="notificationMessage">The JSON-RPC notification message</param>
        public void HandleNotification(JsonRpcNotificationMessage notificationMessage)
        {
            if (IsClosed)
            {
                throw new InvalidOperationException("Transport is closed");
            }

            if (notificationMessage != null && !string.IsNullOrEmpty(notificationMessage.Method))
            {
                Logger.LogDebug(
                    "Handling notification: Method={Method}",
                    notificationMessage.Method
                );

                RaiseOnNotification(notificationMessage);
            }
            else
            {
                Logger.LogWarning(
                    "Received invalid notification with missing method or null notification"
                );
            }
        }

        /// <inheritdoc/>
        protected override async Task OnClosingAsync()
        {
            await ResponseWriter.CompleteAsync();
            await base.OnClosingAsync();
        }
    }
}