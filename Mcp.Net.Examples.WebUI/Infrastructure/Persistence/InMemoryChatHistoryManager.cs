using Mcp.Net.LLM.Models;
using Mcp.Net.LLM.Interfaces;

namespace Mcp.Net.Examples.WebUI.Infrastructure.Persistence;

/// <summary>
/// In-memory implementation of chat history manager
/// </summary>
public class InMemoryChatHistoryManager : IChatHistoryManager
{
    private readonly ILogger<InMemoryChatHistoryManager> _logger;
    private readonly Dictionary<string, List<ChatSessionMetadata>> _userSessions = new();
    private readonly Dictionary<string, List<StoredChatMessage>> _sessionMessages = new();
    private readonly SemaphoreSlim _lock = new(1);

    public InMemoryChatHistoryManager(ILogger<InMemoryChatHistoryManager> logger)
    {
        _logger = logger;
    }

    public async Task<List<ChatSessionMetadata>> GetAllSessionsAsync(string userId)
    {
        _logger.LogInformation("[HISTORY] GetAllSessionsAsync for user {UserId}", userId);
        await _lock.WaitAsync();
        try
        {
            if (!_userSessions.TryGetValue(userId, out var sessions))
            {
                _logger.LogInformation("[HISTORY] No sessions found for user {UserId}", userId);
                return new List<ChatSessionMetadata>();
            }

            _logger.LogInformation("[HISTORY] Found {Count} sessions for user {UserId}: {SessionIds}", 
                sessions.Count, 
                userId, 
                string.Join(", ", sessions.Select(s => s.Id)));

            // Return a copy to prevent external modification
            return sessions.OrderByDescending(s => s.LastUpdatedAt).ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ChatSessionMetadata?> GetSessionMetadataAsync(string sessionId)
    {
        _logger.LogInformation("[HISTORY] GetSessionMetadataAsync for session {SessionId}", sessionId);
        await _lock.WaitAsync();
        try
        {
            // Log the current state of user sessions for debugging
            _logger.LogInformation("[HISTORY] Current user sessions: {UserSessionCounts}", 
                string.Join(", ", _userSessions.Select(kv => $"{kv.Key}: {kv.Value.Count} sessions")));
                
            foreach (var userEntry in _userSessions)
            {
                var userId = userEntry.Key;
                var sessions = userEntry.Value;
                
                _logger.LogInformation("[HISTORY] Checking user {UserId} with {Count} sessions", 
                    userId, sessions.Count);
                    
                var session = sessions.FirstOrDefault(s => s.Id == sessionId);
                if (session != null)
                {
                    _logger.LogInformation("[HISTORY] Found session {SessionId} for user {UserId}", 
                        sessionId, userId);
                        
                    // Return a copy to prevent external modification
                    return new ChatSessionMetadata
                    {
                        Id = session.Id,
                        Title = session.Title,
                        CreatedAt = session.CreatedAt,
                        LastUpdatedAt = session.LastUpdatedAt,
                        Model = session.Model,
                        Provider = session.Provider,
                        SystemPrompt = session.SystemPrompt,
                        LastMessagePreview = session.LastMessagePreview,
                    };
                }
            }

            _logger.LogWarning("[HISTORY] Session {SessionId} not found in any user's sessions", sessionId);
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<ChatSessionMetadata> CreateSessionAsync(
        string userId,
        ChatSessionMetadata metadata
    )
    {
        _logger.LogInformation("[HISTORY] CreateSessionAsync for user {UserId} with metadata: ID={SessionId}, Title={Title}, Model={Model}", 
            userId, metadata.Id, metadata.Title, metadata.Model);
            
        await _lock.WaitAsync();
        try
        {
            // Ensure the user has a session list
            if (!_userSessions.TryGetValue(userId, out var sessions))
            {
                _logger.LogInformation("[HISTORY] Creating new session list for user {UserId}", userId);
                sessions = new List<ChatSessionMetadata>();
                _userSessions[userId] = sessions;
            }
            else
            {
                _logger.LogInformation("[HISTORY] User {UserId} already has {Count} sessions", 
                    userId, sessions.Count);
            }

            // Generate a new ID if not provided
            if (string.IsNullOrEmpty(metadata.Id))
            {
                metadata.Id = Guid.NewGuid().ToString();
                _logger.LogInformation("[HISTORY] Generated new session ID: {SessionId}", metadata.Id);
            }

            // Set default title if not provided
            if (string.IsNullOrEmpty(metadata.Title))
            {
                metadata.Title = $"Chat {sessions.Count + 1}";
                _logger.LogInformation("[HISTORY] Set default title: {Title}", metadata.Title);
            }

            // Set timestamps
            metadata.CreatedAt = DateTime.UtcNow;
            metadata.LastUpdatedAt = DateTime.UtcNow;

            // Add to user's sessions
            sessions.Add(metadata);
            _logger.LogInformation("[HISTORY] Added session to user {UserId}, now has {Count} sessions", 
                userId, sessions.Count);

            // Initialize empty message list
            _sessionMessages[metadata.Id] = new List<StoredChatMessage>();
            _logger.LogInformation("[HISTORY] Initialized empty message list for session {SessionId}", 
                metadata.Id);

            // Log state after creation
            _logger.LogInformation(
                "[HISTORY] Created new chat session {SessionId} for user {UserId}. All users now have: {UserSessions}",
                metadata.Id,
                userId,
                string.Join(", ", _userSessions.Select(kv => $"{kv.Key}: {kv.Value.Count} sessions"))
            );

            return metadata;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateSessionMetadataAsync(ChatSessionMetadata metadata)
    {
        _logger.LogInformation("[HISTORY] UpdateSessionMetadataAsync for session {SessionId} with title: {Title}", 
            metadata.Id, metadata.Title);
            
        await _lock.WaitAsync();
        try
        {
            _logger.LogInformation("[HISTORY] Current user sessions: {UserSessions}", 
                string.Join(", ", _userSessions.Select(kv => $"{kv.Key}: {kv.Value.Count} sessions")));
                
            bool found = false;
            
            foreach (var userEntry in _userSessions)
            {
                var userId = userEntry.Key;
                var sessions = userEntry.Value;
                
                _logger.LogInformation("[HISTORY] Checking user {UserId} for session {SessionId}", 
                    userId, metadata.Id);
                    
                var existingSession = sessions.FirstOrDefault(s => s.Id == metadata.Id);
                if (existingSession != null)
                {
                    found = true;
                    _logger.LogInformation("[HISTORY] Found session {SessionId} in user {UserId}'s sessions, updating...", 
                        metadata.Id, userId);
                        
                    // Log before update
                    _logger.LogInformation("[HISTORY] Before update - Title: {OldTitle}, Model: {OldModel}, Provider: {OldProvider}", 
                        existingSession.Title, existingSession.Model, existingSession.Provider);
                    
                    // Update properties
                    existingSession.Title = metadata.Title;
                    existingSession.LastUpdatedAt = DateTime.UtcNow;
                    existingSession.Model = metadata.Model;
                    existingSession.Provider = metadata.Provider;
                    existingSession.SystemPrompt = metadata.SystemPrompt;
                    existingSession.LastMessagePreview = metadata.LastMessagePreview;

                    // Log after update
                    _logger.LogInformation("[HISTORY] After update - Title: {NewTitle}, Model: {NewModel}, Provider: {NewProvider}", 
                        existingSession.Title, existingSession.Model, existingSession.Provider);
                        
                    _logger.LogInformation("[HISTORY] Updated chat session {SessionId} for user {UserId}", 
                        metadata.Id, userId);
                    return;
                }
            }

            if (!found)
            {
                _logger.LogWarning("[HISTORY] Session {SessionId} not found in any user's sessions", metadata.Id);
                throw new KeyNotFoundException($"Session {metadata.Id} not found");
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteSessionAsync(string sessionId)
    {
        _logger.LogInformation("[HISTORY] DeleteSessionAsync for session {SessionId}", sessionId);
        
        // Log state before deletion
        _logger.LogInformation("[HISTORY] Before deletion - User sessions: {UserSessions}", 
            string.Join(", ", _userSessions.Select(kv => $"{kv.Key}: {kv.Value.Count} sessions ({string.Join(",", kv.Value.Select(s => s.Id))})")));
        
        await _lock.WaitAsync();
        try
        {
            bool found = false;

            // Remove from user sessions
            foreach (var userId in _userSessions.Keys.ToList())
            {
                var sessions = _userSessions[userId];
                _logger.LogInformation("[HISTORY] Checking user {UserId} with {Count} sessions: {SessionIds}", 
                    userId, sessions.Count, string.Join(", ", sessions.Select(s => s.Id)));
                    
                var session = sessions.FirstOrDefault(s => s.Id == sessionId);
                if (session != null)
                {
                    _logger.LogInformation("[HISTORY] Found session {SessionId} in user {UserId}'s sessions, removing...", 
                        sessionId, userId);
                        
                    sessions.Remove(session);
                    found = true;
                    _logger.LogInformation(
                        "[HISTORY] Removed chat session {SessionId} from user {UserId}, user now has {Count} sessions: {SessionIds}",
                        sessionId,
                        userId,
                        sessions.Count,
                        string.Join(", ", sessions.Select(s => s.Id))
                    );
                }
                else
                {
                    _logger.LogInformation("[HISTORY] Session {SessionId} not found in user {UserId}'s sessions", 
                        sessionId, userId);
                }
            }

            // Remove messages
            if (_sessionMessages.ContainsKey(sessionId))
            {
                _logger.LogInformation("[HISTORY] Found messages for session {SessionId}, removing...", sessionId);
                _sessionMessages.Remove(sessionId);
                found = true;
                _logger.LogInformation("[HISTORY] Removed messages for chat session {SessionId}", sessionId);
            }
            else
            {
                _logger.LogInformation("[HISTORY] No messages found for session {SessionId}", sessionId);
            }

            if (!found)
            {
                _logger.LogWarning(
                    "[HISTORY] Attempted to delete non-existent session {SessionId} - it was not found in any user's sessions or message store",
                    sessionId
                );
            }
            
            // Log final state after deletion
            _logger.LogInformation("[HISTORY] After deletion - User sessions: {UserSessions}", 
                string.Join(", ", _userSessions.Select(kv => $"{kv.Key}: {kv.Value.Count} sessions ({string.Join(",", kv.Value.Select(s => s.Id))})")));
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<StoredChatMessage>> GetSessionMessagesAsync(string sessionId)
    {
        _logger.LogInformation("[HISTORY] GetSessionMessagesAsync for session {SessionId}", sessionId);
        await _lock.WaitAsync();
        try
        {
            if (!_sessionMessages.TryGetValue(sessionId, out var messages))
            {
                _logger.LogInformation("[HISTORY] No messages found for session {SessionId}", sessionId);
                return new List<StoredChatMessage>();
            }

            _logger.LogInformation("[HISTORY] Found {Count} messages for session {SessionId}", 
                messages.Count, sessionId);
                
            // Return a copy to prevent external modification
            return messages.OrderBy(m => m.Timestamp).ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task AddMessageAsync(StoredChatMessage message)
    {
        _logger.LogInformation("[HISTORY] AddMessageAsync for session {SessionId}, message type: {Type}", 
            message.SessionId, message.Type);
            
        await _lock.WaitAsync();
        try
        {
            // Ensure the session has a message list
            if (!_sessionMessages.TryGetValue(message.SessionId, out var messages))
            {
                _logger.LogInformation("[HISTORY] Creating new message list for session {SessionId}", 
                    message.SessionId);
                messages = new List<StoredChatMessage>();
                _sessionMessages[message.SessionId] = messages;
            }
            else
            {
                _logger.LogInformation("[HISTORY] Session {SessionId} already has {Count} messages", 
                    message.SessionId, messages.Count);
            }

            // Generate a message ID if not provided
            if (string.IsNullOrEmpty(message.Id))
            {
                message.Id = Guid.NewGuid().ToString();
                _logger.LogInformation("[HISTORY] Generated message ID: {MessageId}", message.Id);
            }

            // Add the message
            messages.Add(message);
            _logger.LogInformation("[HISTORY] Added message {MessageId} to session {SessionId}, now has {Count} messages", 
                message.Id, message.SessionId, messages.Count);

            // Log brief message content for debugging
            var contentPreview = message.Content?.Length > 30 
                ? message.Content.Substring(0, 27) + "..." 
                : message.Content ?? "";
            _logger.LogInformation("[HISTORY] Message content preview: '{ContentPreview}'", contentPreview);

            // Update session metadata
            bool sessionFound = false;
            foreach (var userEntry in _userSessions)
            {
                var userId = userEntry.Key;
                var sessions = userEntry.Value;
                
                var session = sessions.FirstOrDefault(s => s.Id == message.SessionId);
                if (session != null)
                {
                    sessionFound = true;
                    _logger.LogInformation("[HISTORY] Updating session metadata for message in user {UserId}", userId);
                    
                    session.LastUpdatedAt = DateTime.UtcNow;
                    var oldPreview = session.LastMessagePreview;
                    session.LastMessagePreview =
                        message.Content?.Length > 50
                            ? message.Content.Substring(0, 47) + "..."
                            : message.Content ?? "";
                            
                    _logger.LogInformation("[HISTORY] Updated last message preview for session {SessionId}: '{NewPreview}'", 
                        message.SessionId, session.LastMessagePreview);
                }
            }
            
            if (!sessionFound)
            {
                _logger.LogWarning("[HISTORY] Message added to session {SessionId}, but session metadata not found in any user's sessions", 
                    message.SessionId);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task AddMessagesAsync(List<StoredChatMessage> messages)
    {
        if (messages.Count == 0)
        {
            return;
        }

        await _lock.WaitAsync();
        try
        {
            // Group messages by session ID
            var messagesBySession = messages.GroupBy(m => m.SessionId);

            foreach (var group in messagesBySession)
            {
                var sessionId = group.Key;

                // Ensure the session has a message list
                if (!_sessionMessages.TryGetValue(sessionId, out var sessionMessages))
                {
                    sessionMessages = new List<StoredChatMessage>();
                    _sessionMessages[sessionId] = sessionMessages;
                }

                // Add messages
                foreach (var message in group)
                {
                    // Generate a message ID if not provided
                    if (string.IsNullOrEmpty(message.Id))
                    {
                        message.Id = Guid.NewGuid().ToString();
                    }

                    sessionMessages.Add(message);
                }

                // Update session metadata
                var lastMessage = group.OrderByDescending(m => m.Timestamp).FirstOrDefault();
                if (lastMessage != null)
                {
                    foreach (var sessions in _userSessions.Values)
                    {
                        var session = sessions.FirstOrDefault(s => s.Id == sessionId);
                        if (session != null)
                        {
                            session.LastUpdatedAt = DateTime.UtcNow;
                            session.LastMessagePreview =
                                lastMessage.Content?.Length > 50
                                    ? lastMessage.Content.Substring(0, 47) + "..."
                                    : lastMessage.Content ?? "";
                        }
                    }
                }

                _logger.LogDebug(
                    "Added {Count} messages to session {SessionId}",
                    group.Count(),
                    sessionId
                );
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ClearSessionMessagesAsync(string sessionId)
    {
        _logger.LogInformation("[HISTORY] ClearSessionMessagesAsync for session {SessionId}", sessionId);
        await _lock.WaitAsync();
        try
        {
            if (_sessionMessages.TryGetValue(sessionId, out var messages))
            {
                _logger.LogInformation("[HISTORY] Found {Count} messages for session {SessionId}, clearing...", 
                    messages.Count, sessionId);
                messages.Clear();
                _logger.LogInformation("[HISTORY] Cleared all messages for session {SessionId}", sessionId);
            }
            else 
            {
                _logger.LogWarning("[HISTORY] Attempted to clear messages for non-existent session {SessionId}", 
                    sessionId);
            }
        }
        finally
        {
            _lock.Release();
        }
    }
}
