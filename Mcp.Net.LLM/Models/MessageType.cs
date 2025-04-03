namespace Mcp.Net.LLM.Models;

/// <summary>
/// Represents the type of message in the conversation flow
/// </summary>
public enum MessageType
{
    /// <summary>
    /// System messages provide instructions or information from the system
    /// </summary>
    System,

    /// <summary>
    /// User messages are inputs from the end user
    /// </summary>
    User,

    /// <summary>
    /// Assistant messages are text responses from the LLM
    /// </summary>
    Assistant,

    /// <summary>
    /// Tool messages represent tool calls or tool results
    /// </summary>
    Tool,

    /// <summary>
    /// Error messages indicate errors in processing
    /// </summary>
    Error,
}
