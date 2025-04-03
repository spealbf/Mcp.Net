namespace Mcp.Net.WebUi.Adapters.SignalR;

/// <summary>
/// Event args for chat messages
/// </summary>
public class ChatMessageEventArgs : EventArgs
{
    /// <summary>
    /// Chat ID for the session
    /// </summary>
    public string ChatId { get; }

    /// <summary>
    /// Unique message identifier
    /// </summary>
    public string MessageId { get; }

    /// <summary>
    /// Message content
    /// </summary>
    public string Content { get; }

    /// <summary>
    /// Message type (user, assistant, system, etc.)
    /// </summary>
    public string Type { get; }

    public ChatMessageEventArgs(string chatId, string messageId, string content, string type)
    {
        ChatId = chatId;
        MessageId = messageId;
        Content = content;
        Type = type;
    }
}
