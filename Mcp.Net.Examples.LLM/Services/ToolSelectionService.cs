using Mcp.Net.Core.Models.Tools;
using Mcp.Net.Examples.LLM.UI;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.Examples.LLM.Services;

/// <summary>
/// Service responsible for managing tool selection for the chat session
/// </summary>
public class ToolSelectionService
{
    private readonly ILogger<ToolSelectionService> _logger;
    private readonly ToolSelectorUI _selectorUI;
    
    private string[] _selectedToolNames = Array.Empty<string>();
    
    public ToolSelectionService(ILogger<ToolSelectionService> logger)
    {
        _logger = logger;
        _selectorUI = new ToolSelectorUI();
    }
    
    /// <summary>
    /// Prompts the user to select which tools they want to use
    /// </summary>
    /// <param name="availableTools">All tools available from the MCP server</param>
    /// <returns>An array of selected tools</returns>
    public Tool[] PromptForToolSelection(Tool[] availableTools)
    {
        if (availableTools == null || availableTools.Length == 0)
        {
            _logger.LogWarning("No tools available for selection");
            return Array.Empty<Tool>();
        }
        
        _logger.LogInformation("Prompting user to select tools from {ToolCount} available tools", availableTools.Length);
        
        // Show selection UI and get the names of selected tools
        _selectedToolNames = _selectorUI.SelectTools(availableTools);
        
        // Convert selected tool names back to Tool objects
        var selectedTools = availableTools
            .Where(t => _selectedToolNames.Contains(t.Name))
            .ToArray();
            
        _logger.LogInformation("User selected {SelectedCount} tools out of {TotalCount}", 
            selectedTools.Length, availableTools.Length);
            
        return selectedTools;
    }
    
    /// <summary>
    /// Gets all selected tool names
    /// </summary>
    public string[] SelectedToolNames => _selectedToolNames;
    
    /// <summary>
    /// Determines if a specific tool has been selected for use
    /// </summary>
    /// <param name="toolName">Name of the tool to check</param>
    /// <returns>True if the tool is selected, false otherwise</returns>
    public bool IsToolSelected(string toolName)
    {
        return _selectedToolNames.Contains(toolName);
    }
}