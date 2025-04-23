using Mcp.Net.LLM.Interfaces;

namespace Mcp.Net.Examples.LLMConsole.UI;

/// <summary>
/// Simple implementation of IUserInputProvider that uses ChatUI directly
/// </summary>
public class ConsoleUserInputProvider : IUserInputProvider
{
    private readonly ChatUI _chatUI;

    public ConsoleUserInputProvider(ChatUI chatUI)
    {
        _chatUI = chatUI;
    }

    public string GetUserInput()
    {
        return _chatUI.GetUserInput();
    }
}