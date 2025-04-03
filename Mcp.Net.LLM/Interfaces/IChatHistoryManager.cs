using Mcp.Net.LLM.Models;

namespace Mcp.Net.LLM.Interfaces;

/// <summary>
/// Interface for chat history management
/// </summary>
public interface IChatHistoryManager
{
    /// <summary>
    /// Get all chat sessions for a user
    /// </summary>
    Task<List<ChatSessionMetadata>> GetAllSessionsAsync(string userId);

    /// <summary>
    /// Get metadata for a specific chat session
    /// </summary>
    Task<ChatSessionMetadata?> GetSessionMetadataAsync(string sessionId);

    /// <summary>
    /// Create a new chat session
    /// </summary>
    Task<ChatSessionMetadata> CreateSessionAsync(string userId, ChatSessionMetadata metadata);

    /// <summary>
    /// Update chat session metadata
    /// </summary>
    Task UpdateSessionMetadataAsync(ChatSessionMetadata metadata);

    /// <summary>
    /// Delete a chat session and all its messages
    /// </summary>
    Task DeleteSessionAsync(string sessionId);

    /// <summary>
    /// Get all messages for a chat session
    /// </summary>
    Task<List<StoredChatMessage>> GetSessionMessagesAsync(string sessionId);

    /// <summary>
    /// Add a new message to a chat session
    /// </summary>
    Task AddMessageAsync(StoredChatMessage message);

    /// <summary>
    /// Add multiple messages to a chat session
    /// </summary>
    Task AddMessagesAsync(List<StoredChatMessage> messages);

    /// <summary>
    /// Clear all messages from a chat session
    /// </summary>
    Task ClearSessionMessagesAsync(string sessionId);
}