using Mcp.Net.Client.Interfaces;
using Mcp.Net.LLM;
using Mcp.Net.LLM.Interfaces;
using Mcp.Net.LLM.Models;
using Mcp.Net.WebUi.Adapters.Interfaces;
using Mcp.Net.WebUi.Adapters.SignalR;
using Mcp.Net.WebUi.Chat.Interfaces;
using Mcp.Net.WebUi.Hubs;
using Mcp.Net.WebUi.LLM.Factories;
using Microsoft.AspNetCore.SignalR;

namespace Mcp.Net.WebUi.Chat.Factories;

/// <summary>
/// Default LLM settings class to store application-wide defaults
/// </summary>
public class DefaultLlmSettings
{
    public LlmProvider Provider { get; set; } = LlmProvider.Anthropic;
    public string ModelName { get; set; } = "claude-3-5-sonnet-20240620";
    public string DefaultSystemPrompt { get; set; } = "You are a helpful assistant.";
}

/// <summary>
/// Factory for creating chat session components
/// </summary>
public class ChatFactory : IChatFactory
{
    private readonly ILogger<ChatFactory> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly IMcpClient _mcpClient;
    private readonly ToolRegistry _toolRegistry;
    private readonly LlmClientFactory _clientFactory;
    private readonly DefaultLlmSettings _defaultSettings;

    public ChatFactory(
        ILogger<ChatFactory> logger,
        ILoggerFactory loggerFactory,
        IHubContext<ChatHub> hubContext,
        IMcpClient mcpClient,
        ToolRegistry toolRegistry,
        LlmClientFactory clientFactory,
        DefaultLlmSettings defaultSettings
    )
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _hubContext = hubContext;
        _mcpClient = mcpClient;
        _toolRegistry = toolRegistry;
        _clientFactory = clientFactory;
        _defaultSettings = defaultSettings;
    }

    /// <summary>
    /// Create a new SignalR chat adapter with its own dedicated LLM client
    /// </summary>
    public ISignalRChatAdapter CreateSignalRAdapter(
        string sessionId,
        string? model = null,
        string? provider = null,
        string? systemPrompt = null
    )
    {
        // Create chat session logger for this session
        var chatSessionLogger = _loggerFactory.CreateLogger<ChatSession>();

        // Create a new dedicated LLM client for this chat session
        IChatClient sessionClient = CreateClientForSession(sessionId, model, provider);

        // Set system prompt if provided and different from default
        string effectiveSystemPrompt = systemPrompt ?? _defaultSettings.DefaultSystemPrompt;

        if (effectiveSystemPrompt != sessionClient.GetSystemPrompt())
        {
            _logger.LogInformation("Setting system prompt for session {SessionId}", sessionId);
            sessionClient.SetSystemPrompt(effectiveSystemPrompt);
        }

        // Create core chat session (no longer needs inputProvider)
        var chatSession = new ChatSession(
            sessionClient,
            _mcpClient,
            _toolRegistry,
            chatSessionLogger
        );

        // Create adapter logger
        var adapterLogger = _loggerFactory.CreateLogger<SignalRChatAdapter>();

        // Create SignalR adapter
        var adapter = new SignalRChatAdapter(chatSession, _hubContext, adapterLogger, sessionId);

        _logger.LogInformation("Created SignalRChatAdapter for session {SessionId}", sessionId);

        return adapter;
    }

    /// <summary>
    /// Creates a new LLM client instance dedicated to a specific chat session
    /// </summary>
    private IChatClient CreateClientForSession(string sessionId, string? model, string? provider)
    {
        // Determine provider to use (from parameter or default)
        var providerEnum = LlmProvider.Anthropic;
        if (!string.IsNullOrEmpty(provider))
        {
            if (provider.Equals("openai", StringComparison.OrdinalIgnoreCase))
            {
                providerEnum = LlmProvider.OpenAI;
            }
        }
        else
        {
            providerEnum = _defaultSettings.Provider;
        }

        // Determine model to use (from parameter or default)
        var modelName = model ?? _defaultSettings.ModelName;

        // Create client options
        var options = new ChatClientOptions { Model = modelName };

        // Create LLM client through factory
        var client = _clientFactory.Create(providerEnum, options);

        // Register available tools with the client
        client.RegisterTools(_toolRegistry.EnabledTools);

        _logger.LogInformation(
            "Created new {Provider} client with model {Model} for session {SessionId}",
            providerEnum,
            modelName,
            sessionId
        );

        return client;
    }

    /// <summary>
    /// Create session metadata for a new chat
    /// </summary>
    public ChatSessionMetadata CreateSessionMetadata(
        string sessionId,
        string? model = null,
        string? provider = null,
        string? systemPrompt = null
    )
    {
        // Parse provider string if provided
        LlmProvider providerEnum = LlmProvider.Anthropic;
        if (!string.IsNullOrEmpty(provider))
        {
            if (provider.Equals("openai", StringComparison.OrdinalIgnoreCase))
            {
                providerEnum = LlmProvider.OpenAI;
            }
        }

        // Use the specified model or default
        var modelName = model ?? "claude-3-5-sonnet-20240620";

        // Create session metadata
        var metadata = new ChatSessionMetadata
        {
            Id = sessionId,
            Title = $"New Chat {DateTime.UtcNow:g}",
            CreatedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow,
            Model = modelName,
            Provider = providerEnum,
            SystemPrompt = systemPrompt ?? _defaultSettings.DefaultSystemPrompt,
        };

        _logger.LogInformation(
            "Created chat session metadata for session {SessionId} with model {Model}",
            sessionId,
            modelName
        );

        return metadata;
    }
}
