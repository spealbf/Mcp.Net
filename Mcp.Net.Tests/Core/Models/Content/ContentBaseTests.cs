using System.Text.Json;
using FluentAssertions;
using Mcp.Net.Core.Models.Content;

namespace Mcp.Net.Tests.Core.Models.Content;

public class ContentBaseTests
{
    [Fact]
    public void TextContent_Should_Serialize_Correctly()
    {
        // Arrange
        var content = new TextContent { Text = "Hello, world!" };

        // Act
        var json = JsonSerializer.Serialize(content);
        var deserialized = JsonSerializer.Deserialize<ContentBase>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Should().BeOfType<TextContent>();
        var textContent = (TextContent)deserialized!;
        textContent.Type.Should().Be("text");
        textContent.Text.Should().Be("Hello, world!");
    }

    [Fact]
    public void ImageContent_Should_Serialize_Correctly()
    {
        // Arrange
        var content = new ImageContent { Data = "base64encoded", MimeType = "image/png" };

        // Act
        var json = JsonSerializer.Serialize(content);
        var deserialized = JsonSerializer.Deserialize<ContentBase>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Should().BeOfType<ImageContent>();
        var imageContent = (ImageContent)deserialized!;
        imageContent.Type.Should().Be("image");
        imageContent.Data.Should().Be("base64encoded");
        imageContent.MimeType.Should().Be("image/png");
    }

    [Fact]
    public void ContentArray_Should_Deserialize_Mixed_Types_Correctly()
    {
        // Arrange
        var contentArray = new ContentBase[]
        {
            new TextContent { Text = "Text message" },
            new ImageContent { Data = "base64encoded", MimeType = "image/jpeg" },
        };

        // Act
        var json = JsonSerializer.Serialize(contentArray);
        var deserialized = JsonSerializer.Deserialize<ContentBase[]>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Should().HaveCount(2);
        deserialized![0].Should().BeOfType<TextContent>();
        deserialized[1].Should().BeOfType<ImageContent>();
        ((TextContent)deserialized[0]).Text.Should().Be("Text message");
        ((ImageContent)deserialized[1]).Data.Should().Be("base64encoded");
        ((ImageContent)deserialized[1]).MimeType.Should().Be("image/jpeg");
    }
}
