namespace Mcp.Net.WebUi.DTOs;

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
