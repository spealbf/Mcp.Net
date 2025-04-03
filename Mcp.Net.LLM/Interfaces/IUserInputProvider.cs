namespace Mcp.Net.LLM.Interfaces;

/// <summary>
/// Interface for providing user input
/// </summary>
public interface IUserInputProvider
{
    /// <summary>
    /// Gets input from the user
    /// </summary>
    /// <returns>The user input string</returns>
    string GetUserInput();
}
