using Mcp.Net.Core.Models.Tools;

namespace Mcp.Net.Examples.LLM;

public class ToolRegistry
{
    private readonly Dictionary<string, Tool> _toolsByName = new();
    private readonly Dictionary<string, List<Tool>> _toolsByPrefix = new();

    public IReadOnlyList<Tool> AllTools => _toolsByName.Values.ToList();

    public void RegisterTools(IEnumerable<Tool> tools)
    {
        foreach (var tool in tools)
        {
            _toolsByName[tool.Name] = tool;

            // Group by prefix (e.g., "calculator_", "wh40k_")
            string prefix = GetToolPrefix(tool.Name);
            if (!_toolsByPrefix.ContainsKey(prefix))
            {
                _toolsByPrefix[prefix] = new List<Tool>();
            }

            _toolsByPrefix[prefix].Add(tool);
        }

        // Output statistics for each tool group
        foreach (var group in _toolsByPrefix)
        {
            Console.WriteLine($"Found {group.Value.Count} {group.Key.TrimEnd('_')} tools");
        }
    }

    public Tool? GetToolByName(string name)
    {
        return _toolsByName.TryGetValue(name, out var tool) ? tool : null;
    }

    public IReadOnlyList<Tool> GetToolsByPrefix(string prefix)
    {
        return _toolsByPrefix.TryGetValue(prefix, out var tools) ? tools : new List<Tool>();
    }

    private string GetToolPrefix(string name)
    {
        int underscorePos = name.IndexOf('_');
        return underscorePos > 0 ? name.Substring(0, underscorePos + 1) : name;
    }
}
