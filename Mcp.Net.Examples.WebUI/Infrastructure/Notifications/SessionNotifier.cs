using Microsoft.AspNetCore.SignalR;
using Mcp.Net.Examples.WebUI.DTOs;
using Mcp.Net.Examples.WebUI.Hubs;

namespace Mcp.Net.Examples.WebUI.Infrastructure.Notifications;

/// <summary>
/// Service to handle session update notifications
/// </summary>
public class SessionNotifier
{
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<SessionNotifier> _logger;

    public SessionNotifier(
        IHubContext<ChatHub> hubContext,
        ILogger<SessionNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Notify all clients about a session update
    /// </summary>
    public async Task NotifySessionUpdatedAsync(SessionMetadataDto session)
    {
        try
        {
            _logger.LogInformation("[NOTIFIER] Notifying clients about session update: {SessionId}, LastUpdatedAt: {LastUpdatedAt}", 
                session.Id, 
                session.LastUpdatedAt);
                
            // Always update the LastUpdatedAt to ensure proper ordering
            session.LastUpdatedAt = DateTime.UtcNow;
            _logger.LogInformation("[NOTIFIER] Setting LastUpdatedAt to current time: {Time}", session.LastUpdatedAt);
            
            await _hubContext.Clients.All.SendAsync("SessionUpdated", session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NOTIFIER] Error notifying clients about session update: {SessionId}", session.Id);
        }
    }
    
    /// <summary>
    /// Notify all clients about a session deletion
    /// </summary>
    public async Task NotifySessionDeletedAsync(string sessionId)
    {
        try
        {
            _logger.LogInformation("[NOTIFIER] Notifying clients about session deletion: {SessionId}", sessionId);
            await _hubContext.Clients.All.SendAsync("SessionDeleted", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NOTIFIER] Error notifying clients about session deletion: {SessionId}", sessionId);
        }
    }
}