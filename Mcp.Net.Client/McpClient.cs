using System.Text.Json;
using Mcp.Net.Client.Interfaces;
using Mcp.Net.Core.JsonRpc;
using Mcp.Net.Core.Models.Capabilities;
using Mcp.Net.Core.Models.Content;
using Mcp.Net.Core.Models.Messages;
using Mcp.Net.Core.Models.Prompts;
using Mcp.Net.Core.Models.Resources;
using Mcp.Net.Core.Models.Tools;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.Client;

/// <summary>
/// Base implementation of an MCP client.
/// </summary>
public abstract class McpClient : IMcpClient
{
    protected readonly ClientInfo _clientInfo;
    protected readonly Dictionary<string, TaskCompletionSource<object>> _pendingRequests = new();
    protected readonly JsonSerializerOptions _jsonOptions = new() 
    { 
        PropertyNameCaseInsensitive = true 
    };
    protected ServerCapabilities? _serverCapabilities;
    protected readonly ILogger? _logger;

    // These are implemented as regular fields rather than auto-properties to allow derived classes to invoke them
    private Action<JsonRpcResponseMessage>? _onResponse;
    private Action<Exception>? _onError;
    private Action? _onClose;

    // Expose as events for the interface
    public event Action<JsonRpcResponseMessage>? OnResponse
    {
        add => _onResponse += value;
        remove => _onResponse -= value;
    }

    public event Action<Exception>? OnError
    {
        add => _onError += value;
        remove => _onError -= value;
    }

    public event Action? OnClose
    {
        add => _onClose += value;
        remove => _onClose -= value;
    }

    protected McpClient(string clientName, string clientVersion, ILogger? logger = null)
    {
        _clientInfo = new ClientInfo { Name = clientName, Version = clientVersion };
        _logger = logger;
    }

    public abstract Task Initialize();

    public async Task<Tool[]> ListTools()
    {
        if (_serverCapabilities == null)
        {
            throw new InvalidOperationException("Client not initialized. Call Initialize() first.");
        }

        var response = await SendRequest("tools/list");
        var toolsResponse = DeserializeResponse<ListToolsResponse>(response);
        return toolsResponse?.Tools ?? Array.Empty<Tool>();
    }

    public async Task<ToolCallResult> CallTool(string name, object? arguments = null)
    {
        if (_serverCapabilities == null)
        {
            throw new InvalidOperationException("Client not initialized. Call Initialize() first.");
        }

        var response = await SendRequest("tools/call", new { name, arguments });

        var result = DeserializeResponse<ToolCallResult>(response);
        if (result == null)
        {
            throw new Exception("Failed to parse tool response");
        }

        return result;
    }

    public async Task<Resource[]> ListResources()
    {
        if (_serverCapabilities == null)
        {
            throw new InvalidOperationException("Client not initialized. Call Initialize() first.");
        }

        var response = await SendRequest("resources/list");
        var resourcesResponse = DeserializeResponse<ResourcesListResponse>(response);
        return resourcesResponse?.Resources ?? Array.Empty<Resource>();
    }

    public async Task<ResourceContent[]> ReadResource(string uri)
    {
        if (_serverCapabilities == null)
        {
            throw new InvalidOperationException("Client not initialized. Call Initialize() first.");
        }

        var response = await SendRequest("resources/read", new { uri });
        var resourceResponse = DeserializeResponse<ResourceReadResponse>(response);
        return resourceResponse?.Contents
            ?? throw new Exception("Failed to parse resource response");
    }

    public async Task<Prompt[]> ListPrompts()
    {
        if (_serverCapabilities == null)
        {
            throw new InvalidOperationException("Client not initialized. Call Initialize() first.");
        }

        var response = await SendRequest("prompts/list");
        var promptsResponse = DeserializeResponse<PromptsListResponse>(response);
        return promptsResponse?.Prompts ?? Array.Empty<Prompt>();
    }

    public async Task<object[]> GetPrompt(string name)
    {
        if (_serverCapabilities == null)
        {
            throw new InvalidOperationException("Client not initialized. Call Initialize() first.");
        }

        var response = await SendRequest("prompts/get", new { name });
        var promptResponse = DeserializeResponse<PromptsGetResponse>(response);
        return promptResponse?.Messages ?? throw new Exception("Failed to parse prompt response");
    }

    /// <summary>
    /// Sends a request to the server and waits for a response.
    /// </summary>
    protected abstract Task<object> SendRequest(string method, object? parameters = null);

    /// <summary>
    /// Sends a notification to the server.
    /// </summary>
    protected abstract Task SendNotification(string method, object? parameters = null);

    /// <summary>
    /// Helper method to deserialize a response object safely
    /// </summary>
    protected T? DeserializeResponse<T>(object response)
    {
        // If it's already the right type, just return it
        if (response is T typedResponse)
            return typedResponse;

        // Otherwise, serialize to JSON and deserialize to the target type
        var json = JsonSerializer.Serialize(response);
        return JsonSerializer.Deserialize<T>(json, _jsonOptions);
    }

    /// <summary>
    /// Processes a response message from the server.
    /// </summary>
    protected void ProcessResponse(JsonRpcResponseMessage response)
    {
        _logger?.LogDebug("Received response: id={Id}, has error: {HasError}", 
            response.Id, response.Error != null);

        _onResponse?.Invoke(response);

        // Complete pending request if this is a response
        if (_pendingRequests.TryGetValue(response.Id, out var tcs))
        {
            if (response.Error != null)
            {
                _logger?.LogError("Request {Id} failed: {ErrorMessage}", 
                    response.Id, response.Error.Message);
                tcs.SetException(new Exception($"RPC Error: {response.Error.Message}"));
            }
            else if (response.Result != null)
            {
                _logger?.LogDebug("Request {Id} succeeded", response.Id);
                tcs.SetResult(response.Result);
            }
            else
            {
                _logger?.LogDebug("Request {Id} completed with no result", response.Id);
                tcs.SetResult(new { });
            }

            _pendingRequests.Remove(response.Id);
        }
    }

    /// <summary>
    /// Helper method to raise the OnError event.
    /// </summary>
    protected void RaiseOnError(Exception ex)
    {
        _logger?.LogError(ex, "Error in MCP client: {ErrorMessage}", ex.Message);
        _onError?.Invoke(ex);
    }

    /// <summary>
    /// Helper method to raise the OnClose event.
    /// </summary>
    protected void RaiseOnClose()
    {
        _logger?.LogInformation("MCP client connection closed");
        _onClose?.Invoke();
    }

    public abstract void Dispose();
}