namespace Mcp.Net.WebUi.DTOs;

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

    /// <summary>
    /// Agent ID to use for the session, if changing to a specific agent
    /// </summary>
    public string? AgentId { get; set; }
}
