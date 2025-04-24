using Mcp.Net.Core.Models.Tools;
using Mcp.Net.LLM.Tools;
using Microsoft.Extensions.Logging;
using Moq;

namespace Mcp.Net.Tests.LLM.Tools;

/// <summary>
/// Unit tests for the ToolRegistry class
/// </summary>
public class ToolRegistryTests
{
    private readonly Mock<ILogger<ToolRegistry>> _mockLogger;
    private readonly ToolRegistry _registry;

    // Test tools
    private readonly List<Tool> _testTools = new()
    {
        new Tool
        {
            Name = "calculator_add",
            Description = "Adds two numbers",
            InputSchema = System.Text.Json.JsonDocument.Parse("{}").RootElement,
        },
        new Tool
        {
            Name = "calculator_subtract",
            Description = "Subtracts two numbers",
            InputSchema = System.Text.Json.JsonDocument.Parse("{}").RootElement,
        },
        new Tool
        {
            Name = "google_search",
            Description = "Searches the web",
            InputSchema = System.Text.Json.JsonDocument.Parse("{}").RootElement,
        },
    };

    public ToolRegistryTests()
    {
        _mockLogger = new Mock<ILogger<ToolRegistry>>();
        _registry = new ToolRegistry(_mockLogger.Object);

        // Register test tools
        _registry.RegisterTools(_testTools);
    }

    [Fact]
    public void ValidateToolIds_ShouldReturnMissingTools()
    {
        // Arrange
        var toolIds = new[]
        {
            "calculator_add",
            "non_existent_tool",
            "another_missing_tool",
            "google_search",
        };

        // Act
        var missingTools = _registry.ValidateToolIds(toolIds);

        // Assert
        Assert.Equal(2, missingTools.Count);
        Assert.Contains("non_existent_tool", missingTools);
        Assert.Contains("another_missing_tool", missingTools);
    }

    [Fact]
    public void ValidateToolIds_ShouldReturnEmptyListForValidTools()
    {
        // Arrange
        var toolIds = new[] { "calculator_add", "calculator_subtract", "google_search" };

        // Act
        var missingTools = _registry.ValidateToolIds(toolIds);

        // Assert
        Assert.Empty(missingTools);
    }

    [Fact]
    public void GetToolsByPrefix_ShouldReturnCorrectTools()
    {
        // Arrange
        var prefix = "calculator_";

        // Act
        var tools = _registry.GetToolsByPrefix(prefix);

        // Assert
        Assert.Equal(2, tools.Count);
        Assert.All(tools, t => Assert.StartsWith(prefix, t.Name));
    }

    [Fact]
    public void IsToolEnabled_ShouldReturnCorrectState()
    {
        // Arrange - By default all tools are enabled upon registration

        // Act & Assert
        Assert.True(_registry.IsToolEnabled("calculator_add"));

        // Arrange - Disable some tools
        _registry.SetEnabledTools(new[] { "google_search" });

        // Act & Assert
        Assert.False(_registry.IsToolEnabled("calculator_add"));
        Assert.True(_registry.IsToolEnabled("google_search"));
    }

    [Fact]
    public async Task GetToolCategoriesAsync_ShouldReturnAllCategories()
    {
        // Act
        var categories = await _registry.GetToolCategoriesAsync();

        // Assert - Default categories should exist even if empty
        Assert.Contains("math", categories);
        Assert.Contains("search", categories);
        Assert.Contains("utility", categories);
        Assert.Contains("code", categories);
    }

    [Fact]
    public async Task GetToolsByCategoryAsync_ShouldReturnCorrectTools()
    {
        // Act
        var mathTools = await _registry.GetToolsByCategoryAsync("math");

        // Assert
        Assert.Contains("calculator_add", mathTools);
        Assert.Contains("calculator_subtract", mathTools);
        Assert.DoesNotContain("google_search", mathTools);
    }
}
