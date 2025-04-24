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
