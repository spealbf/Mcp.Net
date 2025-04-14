using System.Threading.Tasks;

namespace Mcp.Net.WebUi.LLM.Services;

/// <summary>
/// Service for generating concise titles for chat sessions based on initial messages
/// </summary>
public interface ITitleGenerationService
{
    /// <summary>
    /// Generates a short title (2-4 words) based on an initial chat message
    /// </summary>
    /// <param name="initialMessage">The first message in a chat session</param>
    /// <returns>A concise title for the chat session</returns>
    Task<string> GenerateTitleAsync(string initialMessage);
}
