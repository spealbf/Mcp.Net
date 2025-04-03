namespace Mcp.Net.LLM.Models;


/// <summary>
/// Represents a stored chat message
/// </summary>
public class StoredChatMessage
{
    /// <summary>
    /// Unique identifier for the message
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Chat session this message belongs to
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Message type (user, assistant, system, tool, etc.)
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Message content
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// When the message was created
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Additional data for tool calls or other specialized messages
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}
