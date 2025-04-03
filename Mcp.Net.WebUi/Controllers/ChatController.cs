using Mcp.Net.LLM.Models;
using Mcp.Net.WebUi.Adapters.Interfaces;
using Mcp.Net.WebUi.Adapters.SignalR;
using Mcp.Net.WebUi.Chat.Interfaces;
using Mcp.Net.WebUi.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Mcp.Net.WebUi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly ILogger<ChatController> _logger;
    private readonly IChatRepository _chatRepository;
    private readonly IChatFactory _chatFactory;
    private readonly Dictionary<string, ISignalRChatAdapter> _activeAdapters = new();

    public ChatController(
        ILogger<ChatController> logger,
        IChatRepository chatRepository,
        IChatFactory chatFactory
    )
    {
        _logger = logger;
        _chatRepository = chatRepository;
        _chatFactory = chatFactory;
    }

    /// <summary>
    /// List all chat sessions
    /// </summary>
    [HttpGet("sessions")]
    public async Task<IActionResult> ListSessions()
    {
        try
        {
            var sessions = await _chatRepository.GetAllChatsAsync("default");
            _logger.LogInformation("Listed {Count} sessions via API", sessions.Count);
            return Ok(sessions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing sessions");
            return StatusCode(500, new { error = "Error listing sessions", message = ex.Message });
        }
    }

    /// <summary>
    /// Create a new chat session with optional configuration
    /// </summary>
    [HttpPost("sessions")]
    public async Task<IActionResult> CreateSession([FromBody] SessionCreateDto? options = null)
    {
        try
        {
            // Generate a session ID
            var sessionId = Guid.NewGuid().ToString();

            // Create session metadata
            var metadata = _chatFactory.CreateSessionMetadata(
                sessionId,
                options?.Model,
                options?.Provider,
                options?.SystemPrompt
            );

            // Store in repository
            await _chatRepository.CreateChatAsync(metadata);

            _logger.LogInformation("Created session {SessionId} via API", sessionId);
            return Ok(new { sessionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating session");
            return StatusCode(500, new { error = "Error creating session", message = ex.Message });
        }
    }

    /// <summary>
    /// End a chat session
    /// </summary>
    [HttpDelete("sessions/{sessionId}")]
    public IActionResult EndSession(string sessionId)
    {
        try
        {
            // If we have an active adapter, dispose it
            if (_activeAdapters.TryGetValue(sessionId, out var adapter))
            {
                adapter.Dispose();
                _activeAdapters.Remove(sessionId);
            }

            // Note: We don't delete the session from the repository here
            // That would be handled by a separate "delete" endpoint

            _logger.LogInformation("Ended session {SessionId} via API", sessionId);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Error ending session", message = ex.Message });
        }
    }

    /// <summary>
    /// Delete a chat session and its history
    /// </summary>
    [HttpDelete("sessions/{sessionId}/delete")]
    public async Task<IActionResult> DeleteSession(string sessionId)
    {
        try
        {
            // If we have an active adapter, dispose it
            if (_activeAdapters.TryGetValue(sessionId, out var adapter))
            {
                adapter.Dispose();
                _activeAdapters.Remove(sessionId);
            }

            // Delete the session from the repository
            await _chatRepository.DeleteChatAsync(sessionId);

            _logger.LogInformation(
                "Deleted session {SessionId} and its history via API",
                sessionId
            );
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Error deleting session", message = ex.Message });
        }
    }

    /// <summary>
    /// Send a message to a chat session (alternative to SignalR for clients that can't use it)
    /// </summary>
    [HttpPost("sessions/{sessionId}/messages")]
    public async Task<IActionResult> SendMessage(
        string sessionId,
        [FromBody] ChatMessageDto message
    )
    {
        try
        {
            if (message.Type != "user")
            {
                return BadRequest(new { error = "Only user messages can be sent" });
            }

            // Get or create an adapter
            var adapter = await GetOrCreateAdapterAsync(sessionId);

            // Create message for storage
            var storedMessage = new StoredChatMessage
            {
                Id = message.Id ?? Guid.NewGuid().ToString(),
                SessionId = sessionId,
                Type = "user",
                Content = message.Content,
                Timestamp = DateTime.UtcNow,
            };

            // Store the message
            await _chatRepository.StoreMessageAsync(storedMessage);

            // Process the message with the adapter
            adapter.ProcessUserInput(message.Content);

            _logger.LogInformation("Sent message to session {SessionId} via API", sessionId);
            return Ok();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Session {SessionId} not found", sessionId);
            return NotFound(new { error = "Session not found", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Error sending message", message = ex.Message });
        }
    }

    /// <summary>
    /// Get the current system prompt for a session
    /// </summary>
    [HttpGet("sessions/{sessionId}/system-prompt")]
    public async Task<IActionResult> GetSystemPrompt(string sessionId)
    {
        try
        {
            var systemPrompt = await _chatRepository.GetSystemPromptAsync(sessionId);
            return Ok(new { prompt = systemPrompt });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(
                ex,
                "Session {SessionId} not found when getting system prompt",
                sessionId
            );
            return NotFound(new { error = "Session not found", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system prompt for session {SessionId}", sessionId);
            return StatusCode(
                500,
                new { error = "Error getting system prompt", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Set the system prompt for a session
    /// </summary>
    [HttpPost("sessions/{sessionId}/system-prompt")]
    [HttpPut("sessions/{sessionId}/system-prompt")]
    public async Task<IActionResult> SetSystemPrompt(
        string sessionId,
        [FromBody] SystemPromptDto systemPromptDto
    )
    {
        try
        {
            await _chatRepository.SetSystemPromptAsync(sessionId, systemPromptDto.Prompt);

            // If we have an active adapter, update its system prompt too
            if (
                _activeAdapters.TryGetValue(sessionId, out var adapter)
                && adapter.GetLlmClient() is var client
                && client != null
            )
            {
                client.SetSystemPrompt(systemPromptDto.Prompt);
            }

            _logger.LogInformation("Set system prompt for session {SessionId} via API", sessionId);
            return Ok();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid system prompt for session {SessionId}", sessionId);
            return BadRequest(new { error = "Invalid system prompt", message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(
                ex,
                "Session {SessionId} not found when setting system prompt",
                sessionId
            );
            return NotFound(new { error = "Session not found", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting system prompt for session {SessionId}", sessionId);
            return StatusCode(
                500,
                new { error = "Error setting system prompt", message = ex.Message }
            );
        }
    }

    /// <summary>
    /// Get message history for a session
    /// </summary>
    [HttpGet("sessions/{sessionId}/messages")]
    public async Task<IActionResult> GetSessionMessages(string sessionId)
    {
        try
        {
            var messages = await _chatRepository.GetChatMessagesAsync(sessionId);
            _logger.LogInformation(
                "Retrieved {Count} messages for session {SessionId} via API",
                messages.Count,
                sessionId
            );
            return Ok(messages);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(
                ex,
                "Session {SessionId} not found when getting messages",
                sessionId
            );
            return NotFound(new { error = "Session not found", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting messages for session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Error getting messages", message = ex.Message });
        }
    }

    /// <summary>
    /// Clear message history for a session
    /// </summary>
    [HttpDelete("sessions/{sessionId}/messages")]
    public async Task<IActionResult> ClearSessionMessages(string sessionId)
    {
        try
        {
            await _chatRepository.ClearChatMessagesAsync(sessionId);

            // If we have an active adapter, reset its conversation
            if (_activeAdapters.TryGetValue(sessionId, out var adapter))
            {
                adapter.ResetConversation();
            }

            _logger.LogInformation("Cleared messages for session {SessionId} via API", sessionId);
            return Ok();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(
                ex,
                "Session {SessionId} not found when clearing messages",
                sessionId
            );
            return NotFound(new { error = "Session not found", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing messages for session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Error clearing messages", message = ex.Message });
        }
    }

    /// <summary>
    /// Update session metadata
    /// </summary>
    [HttpPut("sessions/{sessionId}")]
    public async Task<IActionResult> UpdateSession(
        string sessionId,
        [FromBody] SessionUpdateDto sessionUpdateDto
    )
    {
        try
        {
            if (sessionId != sessionUpdateDto.Id)
            {
                return BadRequest(new { error = "Session ID mismatch" });
            }

            await _chatRepository.UpdateChatAsync(sessionUpdateDto);
            _logger.LogInformation("Updated metadata for session {SessionId} via API", sessionId);
            return Ok();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(
                ex,
                "Session {SessionId} not found when updating metadata",
                sessionId
            );
            return NotFound(new { error = "Session not found", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating metadata for session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Error updating session", message = ex.Message });
        }
    }

    /// <summary>
    /// Helper method to get or create an adapter for a session
    /// </summary>
    private async Task<ISignalRChatAdapter> GetOrCreateAdapterAsync(string sessionId)
    {
        // Try to get an existing adapter
        if (_activeAdapters.TryGetValue(sessionId, out var adapter))
        {
            return adapter;
        }

        // Get the session metadata
        var metadata = await _chatRepository.GetChatMetadataAsync(sessionId);
        if (metadata == null)
        {
            throw new KeyNotFoundException($"Session {sessionId} not found");
        }

        // Create a new adapter
        adapter = _chatFactory.CreateSignalRAdapter(
            sessionId,
            metadata.Model,
            metadata.Provider.ToString(),
            metadata.SystemPrompt
        );

        // Subscribe to message events
        adapter.MessageReceived += OnChatMessageReceived;

        // Store in active adapters
        _activeAdapters[sessionId] = adapter;

        // Start the adapter
        adapter.Start();

        return adapter;
    }

    /// <summary>
    /// Handler for chat messages received from an adapter
    /// </summary>
    private async void OnChatMessageReceived(object? sender, ChatMessageEventArgs args)
    {
        try
        {
            // Store the message
            var message = new StoredChatMessage
            {
                Id = args.MessageId,
                SessionId = args.ChatId,
                Type = args.Type,
                Content = args.Content,
                Timestamp = DateTime.UtcNow,
            };

            await _chatRepository.StoreMessageAsync(message);

            // Also update the session metadata with LastUpdatedAt
            var metadata = await _chatRepository.GetChatMetadataAsync(args.ChatId);
            if (metadata != null)
            {
                // Update LastUpdatedAt
                metadata.LastUpdatedAt = DateTime.UtcNow;
                metadata.LastMessagePreview =
                    args.Content.Length > 50 ? args.Content.Substring(0, 47) + "..." : args.Content;

                // Update in repository
                await _chatRepository.UpdateChatMetadataAsync(metadata);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error handling message event for session {SessionId}",
                args.ChatId
            );
        }
    }
}
