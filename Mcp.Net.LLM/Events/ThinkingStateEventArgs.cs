namespace Mcp.Net.LLM.Events;

/// <summary>
/// Event arguments for thinking state changes in the chat session
/// </summary>
public class ThinkingStateEventArgs : EventArgs
{
    /// <summary>
    /// Gets whether the thinking indicator should be shown
    /// </summary>
    public bool IsThinking { get; }

    /// <summary>
    /// Gets the context or reason for the thinking state change
    /// </summary>
    public string Context { get; }

    /// <summary>
    /// Gets the session ID, if applicable
    /// </summary>
    public string? SessionId { get; }

    public ThinkingStateEventArgs(bool isThinking, string context, string? sessionId = null)
    {
        IsThinking = isThinking;
        Context = context;
        SessionId = sessionId;
    }
}