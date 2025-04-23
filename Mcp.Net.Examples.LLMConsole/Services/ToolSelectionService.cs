using Mcp.Net.Examples.LLMConsole.UI;
using Mcp.Net.Core.Models.Tools;

namespace Mcp.Net.Examples.LLMConsole;

public class ToolSelectionService
{
    private readonly ToolSelectorUI _toolSelectorUI;

    public ToolSelectionService()
    {
        _toolSelectorUI = new ToolSelectorUI();
    }

    public Tool[] PromptForToolSelection(Tool[] availableTools)
    {
        Console.WriteLine("Select which tools to enable:");
        Console.WriteLine("Press any key to continue...");
        Console.ReadKey(true);

        var selectedToolNames = _toolSelectorUI.SelectTools(availableTools);
        
        return availableTools
            .Where(t => selectedToolNames.Contains(t.Name))
            .ToArray();
    }
}