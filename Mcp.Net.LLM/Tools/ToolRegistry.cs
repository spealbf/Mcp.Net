using Mcp.Net.Core.Models.Tools;
using Microsoft.Extensions.Logging;

namespace Mcp.Net.LLM.Tools;

public class ToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, Tool> _toolsByName = new();
    private readonly Dictionary<string, List<Tool>> _toolsByPrefix = new();
    private readonly Dictionary<string, List<string>> _toolCategories = new();
    private readonly HashSet<string> _enabledToolNames = new();
    private readonly ILogger? _logger;

    public IReadOnlyList<Tool> AllTools => _toolsByName.Values.ToList();

    public IReadOnlyList<Tool> EnabledTools =>
        _toolsByName.Values.Where(t => _enabledToolNames.Contains(t.Name)).ToList();

    public ToolRegistry(ILogger? logger = null)
    {
        _logger = logger;

        // Initialize common tool categories - derived from tool prefixes by default
        InitializeDefaultCategories();
    }

    private void InitializeDefaultCategories()
    {
        // Standard categories for tools
        _toolCategories["math"] = new List<string>
        {
            "calculator_add",
            "calculator_subtract",
            "calculator_multiply",
            "calculator_divide",
        };

        _toolCategories["search"] = new List<string> { "google_search", "web_scraper" };

        _toolCategories["utility"] = new List<string> { "date_time", "weather", "unit_converter" };

        _toolCategories["code"] = new List<string>
        {
            "code_explainer",
            "code_formatter",
            "code_translator",
        };
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

            // Auto-categorize based on prefix if not already in a category
            AutoCategorizeToolByPrefix(tool.Name, prefix);
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

    private void AutoCategorizeToolByPrefix(string toolName, string prefix)
    {
        // Define mapping of prefixes to categories
        var prefixToCategory = new Dictionary<string, string>
        {
            { "calculator_", "math" },
            { "google_", "search" },
            { "web_", "search" },
            { "date_", "utility" },
            { "weather_", "utility" },
            { "unit_", "utility" },
            { "code_", "code" },
        };

        // Lookup which category this prefix belongs to
        if (prefixToCategory.TryGetValue(prefix, out var category))
        {
            // Create category if it doesn't exist
            if (!_toolCategories.ContainsKey(category))
            {
                _toolCategories[category] = new List<string>();
            }

            // Add tool to category if not already there
            if (!_toolCategories[category].Contains(toolName))
            {
                _toolCategories[category].Add(toolName);
            }
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

    /// <summary>
    /// Gets all available tool categories
    /// </summary>
    /// <returns>A list of tool category names</returns>
    public Task<IEnumerable<string>> GetToolCategoriesAsync()
    {
        return Task.FromResult<IEnumerable<string>>(_toolCategories.Keys.ToList());
    }

    /// <summary>
    /// Gets all tool IDs within a specific category
    /// </summary>
    /// <param name="category">The tool category to get tools from</param>
    /// <returns>A list of tool IDs in the specified category</returns>
    public Task<IEnumerable<string>> GetToolsByCategoryAsync(string category)
    {
        if (_toolCategories.TryGetValue(category.ToLower(), out var tools))
        {
            // Return only enabled tools from the category
            var enabledCategoryTools = tools.Where(t => _enabledToolNames.Contains(t)).ToList();
            return Task.FromResult<IEnumerable<string>>(enabledCategoryTools);
        }

        _logger?.LogWarning("Tool category {Category} not found", category);
        return Task.FromResult<IEnumerable<string>>(Array.Empty<string>());
    }

    /// <summary>
    /// Validates that all specified tool IDs exist
    /// </summary>
    /// <param name="toolIds">The tool IDs to validate</param>
    /// <returns>A list of tool IDs that are missing (not found in the registry)</returns>
    public IReadOnlyList<string> ValidateToolIds(IEnumerable<string> toolIds)
    {
        var availableToolNames = _toolsByName.Keys.ToHashSet();
        var missingToolIds = toolIds.Where(id => !availableToolNames.Contains(id)).ToList();

        if (missingToolIds.Count > 0)
        {
            _logger?.LogWarning(
                "The following tool IDs were not found: {MissingTools}",
                string.Join(", ", missingToolIds)
            );
        }

        return missingToolIds;
    }
}
