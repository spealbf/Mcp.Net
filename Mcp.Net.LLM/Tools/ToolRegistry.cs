using Mcp.Net.Core.Models.Tools;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.LLM.Tools;

public class ToolRegistry
{
    private readonly Dictionary<string, Tool> _toolsByName = new();
    private readonly Dictionary<string, List<Tool>> _toolsByPrefix = new();
    private readonly HashSet<string> _enabledToolNames = new();
    private readonly ILogger? _logger;

    public IReadOnlyList<Tool> AllTools => _toolsByName.Values.ToList();

    public IReadOnlyList<Tool> EnabledTools =>
        _toolsByName.Values.Where(t => _enabledToolNames.Contains(t.Name)).ToList();

    public ToolRegistry(ILogger? logger = null)
    {
        _logger = logger;
    }

    public void RegisterTools(IEnumerable<Tool> tools)
    {
        foreach (var tool in tools)
        {
            _toolsByName[tool.Name] = tool;
            _enabledToolNames.Add(tool.Name); // By default, all tools are enabled

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
            string message = $"Found {group.Value.Count} {group.Key.TrimEnd('_')} tools";
            if (_logger != null)
                _logger.LogInformation(message);
            else
                Console.WriteLine(message);
        }
    }

    /// <summary>
    /// Sets the list of enabled tools by name
    /// </summary>
    /// <param name="enabledToolNames">Names of tools that should be enabled</param>
    public void SetEnabledTools(IEnumerable<string> enabledToolNames)
    {
        _enabledToolNames.Clear();
        foreach (var name in enabledToolNames)
        {
            if (_toolsByName.ContainsKey(name))
            {
                _enabledToolNames.Add(name);
            }
        }

        // Log the number of enabled tools
        string message = $"Enabled {_enabledToolNames.Count} out of {_toolsByName.Count} tools";
        if (_logger != null)
            _logger.LogInformation(message);
        else
            Console.WriteLine(message);
    }

    public Tool? GetToolByName(string name)
    {
        if (!_enabledToolNames.Contains(name))
        {
            return null; // Tool is disabled
        }

        return _toolsByName.TryGetValue(name, out var tool) ? tool : null;
    }

    public IReadOnlyList<Tool> GetToolsByPrefix(string prefix)
    {
        if (!_toolsByPrefix.TryGetValue(prefix, out var tools))
        {
            return new List<Tool>();
        }

        return tools.Where(t => _enabledToolNames.Contains(t.Name)).ToList();
    }

    public string GetToolPrefix(string name)
    {
        int underscorePos = name.IndexOf('_');
        return underscorePos > 0 ? name.Substring(0, underscorePos + 1) : name;
    }

    public bool IsToolEnabled(string name)
    {
        return _enabledToolNames.Contains(name);
    }
}