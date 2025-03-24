using FluentAssertions;
using Mcp.Net.Core.Models.Exceptions;

namespace Mcp.Net.Tests.Core.Models.Exceptions;

public class McpExceptionTests
{
    [Fact]
    public void McpException_Should_Store_ErrorCode()
    {
        // Arrange & Act
        var exception = new McpException(ErrorCode.InvalidRequest, "Invalid request message");

        // Assert
        exception.Code.Should().Be(ErrorCode.InvalidRequest);
        exception.Message.Should().Be("Invalid request message");
        exception.ErrorData.Should().BeNull();
    }

    [Fact]
    public void McpException_Should_Store_ErrorData()
    {
        // Arrange & Act
        var errorData = new { field = "method", problem = "missing" };
        var exception = new McpException(ErrorCode.InvalidParams, "Invalid parameters", errorData);

        // Assert
        exception.Code.Should().Be(ErrorCode.InvalidParams);
        exception.Message.Should().Be("Invalid parameters");
        exception.ErrorData.Should().NotBeNull();
        exception.ErrorData.Should().Be(errorData);
    }

    [Fact]
    public void McpException_Should_Store_InnerException()
    {
        // Arrange
        var innerException = new InvalidOperationException("Original error");

        // Act
        var exception = new McpException(
            ErrorCode.InternalError, 
            "Internal server error", 
            innerException
        );

        // Assert
        exception.Code.Should().Be(ErrorCode.InternalError);
        exception.Message.Should().Be("Internal server error");
        exception.InnerException.Should().NotBeNull();
        exception.InnerException.Should().Be(innerException);
    }
}