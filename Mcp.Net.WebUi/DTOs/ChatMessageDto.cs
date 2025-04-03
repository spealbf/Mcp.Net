using Mcp.Net.LLM.Models;

namespace Mcp.Net.WebUi.DTOs;

/// <summary>
/// DTO for chat messages sent between client and server
/// </summary>
public class ChatMessageDto
{
    /// <summary>
    /// Unique identifier for the message
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Session ID this message belongs to
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Type of message (user, assistant, system)
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Content of the message
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the message was created
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a DTO from an LLM response
    /// </summary>
    public static ChatMessageDto FromLlmResponse(LlmResponse response, string sessionId)
    {
        return new ChatMessageDto
        {
            SessionId = sessionId,
            Type = response.Type.ToString().ToLower(),
            Content = response.Content,
            Timestamp = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Creates an LLM message from this DTO
    /// </summary>
    public LlmMessage ToLlmMessage()
    {
        return new LlmMessage { Type = Enum.Parse<MessageType>(Type, true), Content = Content };
    }
}

/// <summary>
/// DTO for system prompt operations
/// </summary>
public class SystemPromptDto
{
    /// <summary>
    /// Session ID this system prompt belongs to
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// The system prompt text
    /// </summary>
    public string Prompt { get; set; } = string.Empty;
}

/// <summary>
/// DTO for creating a new session
/// </summary>
public class SessionCreateDto
{
    /// <summary>
    /// Model to use for the session
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Provider to use for the session
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// System prompt to use for the session
    /// </summary>
    public string? SystemPrompt { get; set; }
}

/// <summary>
/// DTO for session metadata
/// </summary>
public class SessionMetadataDto
{
    /// <summary>
    /// Unique ID for the session
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Title of the session
    /// </summary>
    public string Title { get; set; } = "New Chat";

    /// <summary>
    /// When the session was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the session was last updated
    /// </summary>
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Model used for the session
    /// </summary>
    public string Model { get; set; } = "default";

    /// <summary>
    /// Provider used for the session
    /// </summary>
    public string Provider { get; set; } = "default";

    /// <summary>
    /// System prompt used for the session
    /// </summary>
    public string SystemPrompt { get; set; } = string.Empty;

    /// <summary>
    /// Preview of the last message
    /// </summary>
    public string? LastMessagePreview { get; set; }
}

/// <summary>
/// DTO for updating session metadata
/// </summary>
public class SessionUpdateDto
{
    /// <summary>
    /// Unique ID for the session
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Title of the session
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Model used for the session
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Provider used for the session
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// System prompt used for the session
    /// </summary>
    public string? SystemPrompt { get; set; }
}
