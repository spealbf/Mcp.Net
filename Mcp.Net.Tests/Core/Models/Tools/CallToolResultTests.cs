using System.Text.Json;
using FluentAssertions;
using Mcp.Net.Core.Models.Content;
using Mcp.Net.Core.Models.Tools;

namespace Mcp.Net.Tests.Core.Models.Tools;

public class CallToolResultTests
{
    [Fact]
    public void CallToolResult_Should_Serialize_Success_Result_Correctly()
    {
        // Arrange
        var result = new ToolCallResult
        {
            Content = new ContentBase[]
            {
                new TextContent { Text = "Operation succeeded" }
            },
            IsError = false
        };

        // Act
        var json = JsonSerializer.Serialize(result);
        var deserialized = JsonSerializer.Deserialize<ToolCallResult>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.IsError.Should().BeFalse();
        deserialized.Content.Should().HaveCount(1);
        deserialized.Content.First().Should().BeOfType<TextContent>();
        ((TextContent)deserialized.Content.First()).Text.Should().Be("Operation succeeded");
    }

    [Fact]
    public void CallToolResult_Should_Serialize_Error_Result_Correctly()
    {
        // Arrange
        var result = new ToolCallResult
        {
            Content = new ContentBase[]
            {
                new TextContent { Text = "Error: Invalid operation" },
                new TextContent { Text = "Stack trace: ..." }
            },
            IsError = true
        };

        // Act
        var json = JsonSerializer.Serialize(result);
        var deserialized = JsonSerializer.Deserialize<ToolCallResult>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.IsError.Should().BeTrue();
        deserialized.Content.Should().HaveCount(2);
        deserialized.Content.Should().AllBeOfType<TextContent>();
        ((TextContent)deserialized.Content.First()).Text.Should().Be("Error: Invalid operation");
        ((TextContent)deserialized.Content.Skip(1).First()).Text.Should().Be("Stack trace: ...");
    }

    [Fact]
    public void CallToolResult_Should_Handle_Empty_Content()
    {
        // Arrange
        var result = new ToolCallResult
        {
            Content = Array.Empty<ContentBase>(),
            IsError = false
        };

        // Act
        var json = JsonSerializer.Serialize(result);
        var deserialized = JsonSerializer.Deserialize<ToolCallResult>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.IsError.Should().BeFalse();
        deserialized.Content.Should().NotBeNull();
        deserialized.Content.Should().BeEmpty();
    }
}