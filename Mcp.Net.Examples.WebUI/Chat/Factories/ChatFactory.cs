using Mcp.Net.Client.Interfaces;
using Mcp.Net.Examples.WebUI.Adapters.Interfaces;
using Mcp.Net.Examples.WebUI.Adapters.SignalR;
using Mcp.Net.Examples.WebUI.Chat.Interfaces;
using Mcp.Net.Examples.WebUI.Hubs;
using Mcp.Net.LLM;
using Mcp.Net.LLM.Interfaces;
using Mcp.Net.LLM.Models;
using Microsoft.AspNetCore.SignalR;

namespace Mcp.Net.Examples.WebUI.Chat.Factories;

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
    private readonly IChatClient _chatClient;

    public ChatFactory(
        ILogger<ChatFactory> logger,
        ILoggerFactory loggerFactory,
        IHubContext<ChatHub> hubContext,
        IMcpClient mcpClient,
        ToolRegistry toolRegistry,
        IChatClient chatClient
    )
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _hubContext = hubContext;
        _mcpClient = mcpClient;
        _toolRegistry = toolRegistry;
        _chatClient = chatClient;
    }

    /// <summary>
    /// Create a new SignalR chat adapter
    /// </summary>
    public ISignalRChatAdapter CreateSignalRAdapter(
        string sessionId,
        string? model = null,
        string? provider = null,
        string? systemPrompt = null
    )
    {
        // Input provider no longer needed after refactoring

        // Create chat session logger for this session
        var chatSessionLogger = _loggerFactory.CreateLogger<ChatSession>();

        // Determine which LLM client to use
        IChatClient sessionClient = _chatClient;

        // For future implementation: create a specific client instance
        // based on model/provider if needed

        // Set system prompt if provided
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            sessionClient.SetSystemPrompt(systemPrompt);
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
        var adapter = new SignalRChatAdapter(
            chatSession,
            _hubContext,
            adapterLogger,
            sessionId
        );

        _logger.LogInformation("Created SignalRChatAdapter for session {SessionId}", sessionId);

        return adapter;
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
            SystemPrompt = systemPrompt ?? _chatClient.GetSystemPrompt(),
        };

        _logger.LogInformation(
            "Created chat session metadata for session {SessionId} with model {Model}",
            sessionId,
            modelName
        );

        return metadata;
    }
}
