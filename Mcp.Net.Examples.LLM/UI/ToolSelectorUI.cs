using Mcp.Net.Core.Models.Tools;

namespace Mcp.Net.Examples.LLM.UI;

/// <summary>
/// Provides UI for selecting tools from a list using keyboard navigation
/// </summary>
public class ToolSelectorUI
{
    private readonly ConsoleColor _defaultColor;
    private readonly ConsoleColor _highlightColor = ConsoleColor.Cyan;
    private readonly ConsoleColor _headerColor = ConsoleColor.Yellow;
    private readonly ConsoleColor _instructionsColor = ConsoleColor.DarkGray;

    public ToolSelectorUI()
    {
        _defaultColor = Console.ForegroundColor;
    }

    // Track screen size for rendering
    private int _menuHeight;
    private int _menuStartPosition;

    /// <summary>
    /// Display a list of tools and let the user select which ones to use
    /// </summary>
    /// <param name="availableTools">List of all available tools</param>
    /// <param name="preSelectedTools">Optional list of pre-selected tool names</param>
    /// <returns>Array of selected tool names</returns>
    public string[] SelectTools(Tool[] availableTools, string[]? preSelectedTools = null)
    {
        if (availableTools == null || availableTools.Length == 0)
        {
            return Array.Empty<string>();
        }

        // Group tools by prefix for easier selection
        var toolGroups = GroupToolsByPrefix(availableTools);

        // Flatten the groups for display
        var flattenedGroups = FlattenToolGroups(toolGroups);

        // Initialize selection state
        var selectedTools = new HashSet<string>(
            preSelectedTools ?? availableTools.Select(t => t.Name)
        );

        // Display UI
        int currentIndex = 0;
        bool selectionComplete = false;

        // Calculate menu height for screen redrawing
        _menuHeight = 3 + 3 + flattenedGroups.Count + 4; // Header (3) + space (1) + tools + instructions (4)

        // Save current cursor position and hide cursor on Windows
        bool cursorVisible = true;

        if (OperatingSystem.IsWindows())
        {
            cursorVisible = Console.CursorVisible;
            Console.CursorVisible = false;
        }

        try
        {
            // First clear the screen to start fresh
            Console.Clear();

            // Remember where we start drawing
            _menuStartPosition = Console.CursorTop;

            // Initial draw
            DrawUI(flattenedGroups, currentIndex, selectedTools);

            while (!selectionComplete)
            {
                var keyInfo = Console.ReadKey(true);

                // Process keypress first
                bool needsRedraw = true;

                switch (keyInfo.Key)
                {
                    case ConsoleKey.UpArrow:
                        currentIndex = Math.Max(0, currentIndex - 1);
                        break;

                    case ConsoleKey.DownArrow:
                        currentIndex = Math.Min(flattenedGroups.Count - 1, currentIndex + 1);
                        break;

                    case ConsoleKey.Spacebar:
                        // Toggle selection for current item
                        if (flattenedGroups[currentIndex].IsGroup)
                        {
                            // Toggle all tools in this group
                            bool allSelected = AreAllToolsInGroupSelected(
                                flattenedGroups[currentIndex].GroupName,
                                flattenedGroups,
                                selectedTools
                            );

                            ToggleToolGroup(
                                flattenedGroups[currentIndex].GroupName,
                                flattenedGroups,
                                selectedTools,
                                !allSelected
                            );
                        }
                        else if (!string.IsNullOrEmpty(flattenedGroups[currentIndex].Tool?.Name))
                        {
                            var toolName = flattenedGroups[currentIndex].Tool!.Name;
                            if (selectedTools.Contains(toolName))
                                selectedTools.Remove(toolName);
                            else
                                selectedTools.Add(toolName);
                        }
                        break;

                    case ConsoleKey.A:
                        // Select all
                        foreach (var tool in availableTools)
                        {
                            selectedTools.Add(tool.Name);
                        }
                        break;

                    case ConsoleKey.N:
                        // Select none
                        selectedTools.Clear();
                        break;

                    case ConsoleKey.Enter:
                        selectionComplete = true;
                        needsRedraw = false;
                        break;

                    case ConsoleKey.Escape:
                        // Cancel and use all tools
                        selectedTools.Clear();
                        foreach (var tool in availableTools)
                        {
                            selectedTools.Add(tool.Name);
                        }
                        selectionComplete = true;
                        needsRedraw = false;
                        break;

                    default:
                        needsRedraw = false;
                        break;
                }

                // Only redraw if needed
                if (needsRedraw)
                {
                    DrawUI(flattenedGroups, currentIndex, selectedTools);
                }
            }
        }
        finally
        {
            // Restore cursor on Windows
            if (OperatingSystem.IsWindows())
            {
                Console.CursorVisible = cursorVisible;
            }
        }

        return selectedTools.ToArray();
    }

    private void DrawHeader()
    {
        Console.ForegroundColor = _headerColor;
        Console.WriteLine("╭──────────────────────────────────────────────╮");
        Console.WriteLine("│            SELECT TOOLS TO ENABLE            │");
        Console.WriteLine("╰──────────────────────────────────────────────╯");
        Console.WriteLine();
        Console.ForegroundColor = _defaultColor;
    }

    private void DrawInstructions()
    {
        Console.WriteLine();
        Console.ForegroundColor = _instructionsColor;
        Console.WriteLine("╭───────────────────────────────────────────────╮");
        Console.WriteLine("│ Navigate: ↑/↓  Select: SPACE  All: A  None: N │");
        Console.WriteLine("│ Confirm: ENTER  Cancel: ESC                   │");
        Console.WriteLine("╰───────────────────────────────────────────────╯");
        Console.ForegroundColor = _defaultColor;
    }

    /// <summary>
    /// Draws the entire UI in one pass to avoid flickering
    /// </summary>
    private void DrawUI(
        List<ToolGroupItem> toolGroups,
        int currentIndex,
        HashSet<string> selectedTools
    )
    {
        // Position cursor at start position
        Console.SetCursorPosition(0, _menuStartPosition);

        // Clear all content with spaces
        for (int i = 0; i < _menuHeight; i++)
        {
            Console.SetCursorPosition(0, _menuStartPosition + i);
            Console.Write(new string(' ', Console.WindowWidth));
        }

        // Reset cursor and draw everything
        Console.SetCursorPosition(0, _menuStartPosition);

        DrawHeader();
        DrawToolList(toolGroups, currentIndex, selectedTools);
        DrawInstructions();
    }

    private void DrawToolList(
        List<ToolGroupItem> toolGroups,
        int currentIndex,
        HashSet<string> selectedTools
    )
    {
        for (int i = 0; i < toolGroups.Count; i++)
        {
            bool isHighlighted = i == currentIndex;
            var item = toolGroups[i];

            if (item.IsGroup)
            {
                DrawGroupHeader(
                    item.GroupName,
                    isHighlighted,
                    AreAllToolsInGroupSelected(item.GroupName, toolGroups, selectedTools)
                );
            }
            else if (item.Tool != null)
            {
                bool isSelected = selectedTools.Contains(item.Tool.Name);
                DrawToolItem(item.Tool, isHighlighted, isSelected);
            }
        }
    }

    private void DrawGroupHeader(string groupName, bool isHighlighted, bool allSelected)
    {
        Console.ForegroundColor = isHighlighted ? _highlightColor : _headerColor;

        string formattedName = groupName;
        if (groupName.EndsWith("_"))
            formattedName = groupName.TrimEnd('_');

        string selectionIndicator = allSelected ? "[*]" : "[ ]";
        Console.WriteLine($"{selectionIndicator} {formattedName.ToUpper()} TOOLS");

        Console.ForegroundColor = _defaultColor;
    }

    private void DrawToolItem(Tool tool, bool isHighlighted, bool isSelected)
    {
        Console.ForegroundColor = isHighlighted ? _highlightColor : _defaultColor;

        string selectionIndicator = isSelected ? "[*]" : "[ ]";
        string formattedName = tool.Name;

        // If the tool name has a prefix (like calculator_add), remove the prefix
        int underscorePos = tool.Name.IndexOf('_');
        if (underscorePos > 0)
        {
            formattedName = tool.Name.Substring(underscorePos + 1);
        }

        Console.Write($"  {selectionIndicator} {formattedName}");

        // If we're highlighted, show the description
        if (isHighlighted && !string.IsNullOrEmpty(tool.Description))
        {
            Console.ForegroundColor = _instructionsColor;
            Console.Write($" - {tool.Description}");
        }

        Console.WriteLine();
        Console.ForegroundColor = _defaultColor;
    }

    private bool AreAllToolsInGroupSelected(
        string groupName,
        List<ToolGroupItem> flattenedGroups,
        HashSet<string> selectedTools
    )
    {
        var toolsInGroup = flattenedGroups
            .Where(item =>
                !item.IsGroup
                && item.Tool != null
                && (item.Tool.Name.StartsWith(groupName) || groupName == "All")
            )
            .Select(item => item.Tool!.Name)
            .ToList();

        return toolsInGroup.Count > 0 && toolsInGroup.All(t => selectedTools.Contains(t));
    }

    private void ToggleToolGroup(
        string groupName,
        List<ToolGroupItem> flattenedGroups,
        HashSet<string> selectedTools,
        bool selectAll
    )
    {
        var toolsInGroup = flattenedGroups
            .Where(item =>
                !item.IsGroup
                && item.Tool != null
                && (item.Tool.Name.StartsWith(groupName) || groupName == "All")
            )
            .Select(item => item.Tool!)
            .ToList();

        foreach (var tool in toolsInGroup)
        {
            if (selectAll)
            {
                selectedTools.Add(tool.Name);
            }
            else
            {
                selectedTools.Remove(tool.Name);
            }
        }
    }

    private Dictionary<string, List<Tool>> GroupToolsByPrefix(Tool[] tools)
    {
        var result = new Dictionary<string, List<Tool>>();

        // Special "All" group
        result["All"] = tools.ToList();

        foreach (var tool in tools)
        {
            string prefix = GetToolPrefix(tool.Name);

            if (!result.ContainsKey(prefix))
            {
                result[prefix] = new List<Tool>();
            }

            result[prefix].Add(tool);
        }

        return result;
    }

    private List<ToolGroupItem> FlattenToolGroups(Dictionary<string, List<Tool>> toolGroups)
    {
        var result = new List<ToolGroupItem>();

        // Add "All" group first
        if (toolGroups.ContainsKey("All"))
        {
            result.Add(new ToolGroupItem { IsGroup = true, GroupName = "All" });
        }

        // Add other groups
        foreach (var group in toolGroups.Where(g => g.Key != "All"))
        {
            // Add group header
            result.Add(new ToolGroupItem { IsGroup = true, GroupName = group.Key });

            // Add tools in group
            foreach (var tool in group.Value)
            {
                result.Add(new ToolGroupItem { IsGroup = false, Tool = tool });
            }
        }

        return result;
    }

    private string GetToolPrefix(string name)
    {
        int underscorePos = name.IndexOf('_');
        return underscorePos > 0 ? name.Substring(0, underscorePos + 1) : name;
    }

    private class ToolGroupItem
    {
        public bool IsGroup { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public Tool? Tool { get; set; }
    }
}
