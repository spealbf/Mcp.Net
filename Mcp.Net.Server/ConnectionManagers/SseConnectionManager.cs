using System.Text.Json;
using Mcp.Net.Core.Interfaces;
using Mcp.Net.Core.JsonRpc;
using Mcp.Net.Core.Models.Exceptions;
using Mcp.Net.Server.Interfaces;
using Mcp.Net.Server.Transport.Sse;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.Server.ConnectionManagers;

/// <summary>
/// Manages SSE connections for server-client communication
/// </summary>
public class SseConnectionManager : IConnectionManager
{
    private readonly InMemoryConnectionManager _connectionManager;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<SseConnectionManager> _logger;
    private readonly McpServer _server;

    /// <summary>
    /// Initializes a new instance of the <see cref="SseConnectionManager"/> class
    /// </summary>
    /// <param name="server">The MCP server instance</param>
    /// <param name="loggerFactory">Logger factory for creating loggers</param>
    /// <param name="connectionTimeout">Optional timeout for stale connections</param>
    public SseConnectionManager(
        McpServer server,
        ILoggerFactory loggerFactory,
        TimeSpan? connectionTimeout = null
    )
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = loggerFactory.CreateLogger<SseConnectionManager>();

        // Use the InMemoryConnectionManager for actual connection tracking
        _connectionManager = new InMemoryConnectionManager(loggerFactory, connectionTimeout);
    }

    /// <inheritdoc />
    public Task<ITransport?> GetTransportAsync(string sessionId)
    {
        return _connectionManager.GetTransportAsync(sessionId);
    }

    /// <inheritdoc />
    public Task RegisterTransportAsync(string sessionId, ITransport transport)
    {
        return _connectionManager.RegisterTransportAsync(sessionId, transport);
    }

    /// <inheritdoc />
    public Task<bool> RemoveTransportAsync(string sessionId)
    {
        return _connectionManager.RemoveTransportAsync(sessionId);
    }

    /// <inheritdoc />
    public Task CloseAllConnectionsAsync()
    {
        return _connectionManager.CloseAllConnectionsAsync();
    }

    /// <summary>
    /// Gets a transport by session ID (synchronous version for backward compatibility)
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <returns>The transport, or null if not found</returns>
    public SseTransport? GetTransport(string sessionId)
    {
        var transport = _connectionManager.GetTransportAsync(sessionId).GetAwaiter().GetResult();
        return transport as SseTransport;
    }

    /// <summary>
    /// Registers a transport with the connection manager (synchronous version)
    /// </summary>
    /// <param name="transport">The transport to register</param>
    public void RegisterTransport(SseTransport transport)
    {
        _connectionManager
            .RegisterTransportAsync(transport.SessionId, transport)
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>
    /// Removes a transport from the connection manager (synchronous version)
    /// </summary>
    /// <param name="sessionId">The session ID to remove</param>
    /// <returns>True if the transport was found and removed, false otherwise</returns>
    public bool RemoveTransport(string sessionId)
    {
        return _connectionManager.RemoveTransportAsync(sessionId).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Creates, initializes, and manages an SSE connection from an HTTP context
    /// </summary>
    /// <param name="context">The HTTP context for the SSE connection</param>
    /// <returns>A task that completes when the connection is closed</returns>
    public async Task HandleSseConnectionAsync(HttpContext context)
    {
        // Create and set up logger with connection details
        var logger = _loggerFactory.CreateLogger("SSE");
        string clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        logger.LogInformation("New SSE connection from {ClientIp}", clientIp);

        var transport = CreateTransport(context);

        await RegisterTransportAsync(transport.SessionId, transport);

        var sessionId = transport.SessionId;
        logger.LogInformation("Registered SSE transport with session ID {SessionId}", sessionId);

        using (
            logger.BeginScope(
                new Dictionary<string, object>
                {
                    ["SessionId"] = sessionId,
                    ["ClientIp"] = clientIp,
                }
            )
        )
        {
            try
            {
                await _server.ConnectAsync(transport);
                logger.LogInformation("Server connected to transport");

                // Keep the connection alive until client disconnects
                await Task.Delay(-1, context.RequestAborted);
            }
            catch (TaskCanceledException)
            {
                logger.LogInformation("SSE connection closed");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in SSE connection");
            }
            finally
            {
                await transport.CloseAsync();
                logger.LogInformation("SSE transport closed");
            }
        }
    }

    /// <summary>
    /// Process JSON-RPC messages from client
    /// </summary>
    /// <param name="context">The HTTP context containing the message</param>
    /// <returns>A task that completes when the message is processed</returns>
    public async Task HandleMessageAsync(HttpContext context)
    {
        var logger = _loggerFactory.CreateLogger("MessageEndpoint");
        var sessionId = context.Request.Query["sessionId"].ToString();

        if (string.IsNullOrEmpty(sessionId))
        {
            logger.LogWarning("Message received without session ID");
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = "Missing sessionId" });
            return;
        }

        using (logger.BeginScope(new Dictionary<string, object> { ["SessionId"] = sessionId }))
        {
            var transport = await GetTransportAsync(sessionId) as SseTransport;
            if (transport == null)
            {
                logger.LogWarning("Session not found for ID {SessionId}", sessionId);
                context.Response.StatusCode = 404;
                await context.Response.WriteAsJsonAsync(new { error = "Session not found" });
                return;
            }

            try
            {
                var request = await JsonSerializer.DeserializeAsync<JsonRpcRequestMessage>(
                    context.Request.Body
                );

                if (request == null)
                {
                    logger.LogError("Invalid JSON-RPC request format");
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync(
                        new JsonRpcError
                        {
                            Code = (int)ErrorCode.InvalidRequest,
                            Message = "Invalid request",
                        }
                    );
                    return;
                }

                logger.LogDebug(
                    "JSON-RPC Request: Method={Method}, Id={Id}",
                    request.Method,
                    request.Id ?? "null"
                );

                if (request.Params != null)
                {
                    logger.LogTrace(
                        "Request params: {Params}",
                        JsonSerializer.Serialize(request.Params)
                    );
                }

                // Process the request through the transport
                transport.HandlePostMessage(request);

                // Return 202 Accepted immediately
                context.Response.StatusCode = 202;
                await context.Response.WriteAsJsonAsync(new { status = "accepted" });
            }
            catch (JsonException ex)
            {
                await HandleJsonParsingError(context, ex, logger);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing message");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(
                    new JsonRpcError
                    {
                        Code = (int)ErrorCode.InternalError,
                        Message = "Internal server error",
                    }
                );
            }
        }
    }

    /// <summary>
    /// Creates a new SSE transport from an HTTP context
    /// </summary>
    /// <param name="context">The HTTP context</param>
    /// <returns>The created transport</returns>
    private SseTransport CreateTransport(HttpContext context)
    {
        var responseWriter = new HttpResponseWriter(
            context.Response,
            _loggerFactory.CreateLogger<HttpResponseWriter>()
        );

        return new SseTransport(responseWriter, _loggerFactory.CreateLogger<SseTransport>());
    }

    /// <summary>
    /// Handles JSON parsing errors with detailed logging
    /// </summary>
    private static async Task HandleJsonParsingError(
        HttpContext context,
        JsonException ex,
        ILogger logger
    )
    {
        logger.LogError(ex, "JSON parsing error");

        try
        {
            // Rewind the request body stream to try to read it as raw data for debugging
            context.Request.Body.Position = 0;
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            string rawContent = await reader.ReadToEndAsync();

            string truncatedContent =
                rawContent.Length > 300 ? rawContent.Substring(0, 297) + "..." : rawContent;

            logger.LogDebug("Raw JSON content that failed parsing: {Content}", truncatedContent);

            try
            {
                var doc = JsonDocument.Parse(rawContent);
                if (doc.RootElement.TryGetProperty("id", out var idElement))
                {
                    string idValue =
                        idElement.ValueKind == JsonValueKind.Number
                            ? idElement.GetRawText()
                            : idElement.ToString();
                    logger.LogInformation("Request had ID: {Id}", idValue);
                }
            }
            catch (JsonException)
            {
                logger.LogError("Content is not valid JSON");
            }

            context.Request.Body.Position = 0;
        }
        catch (Exception logEx)
        {
            logger.LogError(logEx, "Failed to log request content");
        }

        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(
            new JsonRpcError { Code = (int)ErrorCode.ParseError, Message = "Parse error" }
        );
    }
}
