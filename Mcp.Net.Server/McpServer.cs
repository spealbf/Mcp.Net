using System.Text.Json;
using Mcp.Net.Core.Interfaces;
using Mcp.Net.Core.JsonRpc;
using Mcp.Net.Core.Models.Capabilities;
using Mcp.Net.Core.Models.Content;
using Mcp.Net.Core.Models.Exceptions;
using Mcp.Net.Core.Models.Messages;
using Mcp.Net.Core.Models.Tools;
using Mcp.Net.Server.Interfaces;
using Mcp.Net.Server.Logging;

public class McpServer : IMcpServer
{
    // Dictionary to store method handlers that take a JSON string parameter
    private readonly Dictionary<string, Func<string, Task<object>>> _methodHandlers = new();
    private readonly Dictionary<string, Tool> _tools = new();
    private readonly Dictionary<string, Func<JsonElement?, Task<ToolCallResult>>> _toolHandlers =
        new();
    private ITransport? _transport;

    private readonly ServerInfo _serverInfo;
    private readonly ServerCapabilities _capabilities;
    private readonly string? _instructions;

    // No delegate needed with our simplified approach

    public McpServer(ServerInfo serverInfo, ServerOptions? options = null)
    {
        _serverInfo = serverInfo;
        _capabilities = options?.Capabilities ?? new ServerCapabilities();

        // Ensure all capabilities are initialized
        if (_capabilities.Tools == null)
            _capabilities.Tools = new { };

        if (_capabilities.Resources == null)
            _capabilities.Resources = new { };

        if (_capabilities.Prompts == null)
            _capabilities.Prompts = new { };

        _instructions = options?.Instructions;
        InitializeDefaultMethods();

        Logger.Debug(
            "McpServer created with server info: {Name} {Version}",
            serverInfo.Name,
            serverInfo.Version
        );
    }

    public async Task ConnectAsync(ITransport transport)
    {
        _transport = transport;

        // Set up event handlers
        transport.OnRequest += HandleRequest;
        transport.OnNotification += HandleNotification;
        transport.OnError += HandleTransportError;
        transport.OnClose += HandleTransportClose;

        Logger.Information("MCP server connecting to transport");

        // Start the transport
        await transport.StartAsync();
    }

    private void HandleRequest(JsonRpcRequestMessage request)
    {
        Logger.Debug($"Received request: ID={request.Id}, Method={request.Method}");
        _ = ProcessRequestAsync(request);
    }

    private void HandleNotification(JsonRpcNotificationMessage notification)
    {
        Logger.Debug($"Received notification: Method={notification.Method}");
        // Process notifications if needed
    }

    private async Task ProcessRequestAsync(JsonRpcRequestMessage request)
    {
        Logger.Debug($"Processing request: Method={request.Method}, ID={request.Id}");
        var response = await ProcessJsonRpcRequest(request);

        // Send the response via the transport
        if (_transport != null)
        {
            Logger.Debug(
                "Sending response: ID={Id}, HasResult={HasResult}, HasError={HasError}",
                response.Id,
                response.Result != null,
                response.Error != null
            );

            // Pass the response directly to the transport
            await _transport.SendAsync(response);
        }
    }

    private void HandleTransportError(Exception ex)
    {
        Logger.Error("Transport error", ex);
    }

    private void HandleTransportClose()
    {
        Logger.Information("Transport connection closed");
    }

    private void InitializeDefaultMethods()
    {
        // Register methods with their strongly-typed request handlers
        RegisterMethod<InitializeRequest>("initialize", HandleInitialize);
        RegisterMethod<ListToolsRequest>("tools/list", HandleToolsList);
        RegisterMethod<ToolCallRequest>("tools/call", HandleToolCall);
        RegisterMethod<ResourcesListRequest>("resources/list", HandleResourcesList);
        RegisterMethod<ResourcesReadRequest>("resources/read", HandleResourcesRead);
        RegisterMethod<PromptsListRequest>("prompts/list", HandlePromptsList);
        RegisterMethod<PromptsGetRequest>("prompts/get", HandlePromptsGet);

        Logger.Debug("Default MCP methods registered");
    }

    private Task<object> HandleInitialize(InitializeRequest request)
    {
        Logger.Information("Handling initialize request");

        return Task.FromResult<object>(
            new
            {
                protocolVersion = "2024-11-05", // Using latest from spec
                capabilities = _capabilities,
                serverInfo = _serverInfo,
                instructions = _instructions,
            }
        );
    }

    private Task<object> HandleToolsList(ListToolsRequest _)
    {
        Logger.Debug("Handling tools/list request, returning {Count} tools", _tools.Count);
        return Task.FromResult<object>(new { tools = _tools.Values });
    }

    private async Task<object> HandleToolCall(ToolCallRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Name))
            {
                Logger.Warning("Tool call received with empty tool name");
                throw new McpException(ErrorCode.InvalidParams, "Tool name cannot be empty");
            }

            if (!_toolHandlers.TryGetValue(request.Name, out var handler))
            {
                Logger.Warning("Tool call received for unknown tool: {ToolName}", request.Name);
                throw new McpException(ErrorCode.InvalidParams, $"Tool not found: {request.Name}");
            }

            // Extract arguments from the request if they exist
            JsonElement? argumentsElement = request.GetArguments();

            Logger.Information("Executing tool: {ToolName}", request.Name);
            var response = await handler(argumentsElement);

            // Log tool execution result
            if (response.IsError)
            {
                string errorMessage = response.Content?.FirstOrDefault() is TextContent textContent
                    ? textContent.Text
                    : "Unknown error";

                Logger.Warning(
                    "Tool {ToolName} execution failed: {ErrorMessage}",
                    request.Name,
                    errorMessage
                );
            }
            else
            {
                Logger.Information("Tool {ToolName} executed successfully", request.Name);
            }

            return response;
        }
        catch (JsonException ex)
        {
            // Handle JSON parsing errors
            Logger.Error("JSON parsing error in tool call", ex);
            return new ToolCallResult
            {
                IsError = true,
                Content = new[]
                {
                    new TextContent { Text = $"Invalid tool call parameters: {ex.Message}" },
                },
            };
        }
        catch (McpException ex)
        {
            // Propagate MCP exceptions with their error codes
            Logger.Warning("MCP exception in tool call: {Message}", ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            // Convert other exceptions to a tool response with error
            Logger.Error("Unexpected error in tool call", ex);
            return new ToolCallResult
            {
                IsError = true,
                Content = new[]
                {
                    new TextContent { Text = $"Error executing tool: {ex.Message}" },
                },
            };
        }
    }

    private Task<object> HandleResourcesList(ResourcesListRequest _)
    {
        Logger.Debug("Handling resources/list request");
        return Task.FromResult<object>(new { resources = Array.Empty<object>() });
    }

    private Task<object> HandleResourcesRead(ResourcesReadRequest request)
    {
        Logger.Debug("Handling resources/read request");

        if (string.IsNullOrEmpty(request.Uri))
        {
            throw new McpException(ErrorCode.InvalidParams, "Invalid URI");
        }

        Logger.Information("Resource read requested for URI: {Uri}", request.Uri);
        throw new McpException(ErrorCode.ResourceNotFound, $"Resource not found: {request.Uri}");
    }

    private Task<object> HandlePromptsList(PromptsListRequest _)
    {
        Logger.Debug("Handling prompts/list request");
        return Task.FromResult<object>(new { prompts = Array.Empty<object>() });
    }

    private Task<object> HandlePromptsGet(PromptsGetRequest request)
    {
        Logger.Debug("Handling prompts/get request");

        if (string.IsNullOrEmpty(request.Name))
        {
            throw new McpException(ErrorCode.InvalidParams, "Invalid prompt name");
        }

        Logger.Information("Prompt requested: {Name}", request.Name);
        throw new McpException(ErrorCode.PromptNotFound, $"Prompt not found: {request.Name}");
    }

    /// <summary>
    /// Register a method handler for a specific request type
    /// </summary>
    private void RegisterMethod<TRequest>(string methodName, Func<TRequest, Task<object>> handler)
        where TRequest : IMcpRequest
    {
        // Store a function that takes a JSON string, deserializes it and calls the handler
        _methodHandlers[methodName] = async (jsonParams) =>
        {
            try
            {
                // Deserialize the JSON string to our request type
                TRequest? request;
                if (string.IsNullOrEmpty(jsonParams))
                {
                    // Create empty instance for parameter-less requests
                    request = Activator.CreateInstance<TRequest>();
                }
                else
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    request = JsonSerializer.Deserialize<TRequest>(jsonParams, options);
                }

                if (request == null)
                {
                    throw new McpException(
                        ErrorCode.InvalidParams,
                        $"Failed to deserialize parameters for {methodName}"
                    );
                }

                // Call the handler with the typed request
                return await handler(request);
            }
            catch (JsonException ex)
            {
                Logger.Error($"JSON deserialization error for {methodName}: {ex.Message}");
                throw new McpException(
                    ErrorCode.InvalidParams,
                    $"Invalid parameters: {ex.Message}"
                );
            }
        };

        Logger.Debug("Registered method: {MethodName}", methodName);
    }

    public void RegisterTool(
        string name,
        string? description,
        JsonElement inputSchema,
        Func<JsonElement?, Task<ToolCallResult>> handler
    )
    {
        var tool = new Tool
        {
            Name = name,
            Description = description,
            InputSchema = inputSchema,
        };

        _tools[name] = tool;
        _toolHandlers[name] = async (args) =>
        {
            try
            {
                Logger.Information("Tool {ToolName} invoked", name);
                return await handler(args);
            }
            catch (Exception ex)
            {
                // Convert any exceptions in the tool handler to a proper CallToolResult with IsError=true
                Logger.Error("Error in tool handler: {ToolName}", ex, name);
                return new ToolCallResult
                {
                    IsError = true,
                    Content = new[]
                    {
                        new TextContent { Text = ex.Message },
                        new TextContent { Text = $"Stack trace:\n{ex.StackTrace}" },
                    },
                };
            }
        };

        // Ensure tools capability is registered
        if (_capabilities.Tools == null)
        {
            _capabilities.Tools = new { };
        }

        Logger.Information(
            "Registered tool: {ToolName} - {Description}",
            name,
            description ?? "No description"
        );
    }

    public async Task<JsonRpcResponseMessage> ProcessJsonRpcRequest(JsonRpcRequestMessage request)
    {
        if (!_methodHandlers.TryGetValue(request.Method, out var handler))
        {
            Logger.Warning("Method not found: {Method}", request.Method);
            return CreateErrorResponse(request.Id, ErrorCode.MethodNotFound, "Method not found");
        }

        try
        {
            // Convert params to a string for consistent handling
            string paramsJson = "";
            if (request.Params != null)
            {
                // Serialize params object to JSON string for handler
                paramsJson = JsonSerializer.Serialize(request.Params);
            }

            // Call the handler with the JSON string
            var result = await handler(paramsJson);

            Logger.Debug($"Request {request.Id} ({request.Method}) handled successfully");
            // We can pass the result object directly now
            return new JsonRpcResponseMessage("2.0", request.Id, result, null);
        }
        catch (McpException ex)
        {
            Logger.Warning(
                $"MCP exception handling request {request.Id} ({request.Method}): {ex.Message}"
            );
            return CreateErrorResponse(request.Id, ex.Code, ex.Message, ex.Data);
        }
        catch (Exception ex)
        {
            Logger.Error($"Error handling request {request.Id} ({request.Method})", ex);
            return CreateErrorResponse(request.Id, ErrorCode.InternalError, ex.Message);
        }
    }

    /// <summary>
    /// Helper method to create error responses
    /// </summary>
    private JsonRpcResponseMessage CreateErrorResponse(
        string id,
        ErrorCode code,
        string message,
        object? data = null
    )
    {
        var error = new JsonRpcError
        {
            Code = (int)code,
            Message = message,
            Data = data,
        };
        return new JsonRpcResponseMessage("2.0", id, null, error);
    }
}
