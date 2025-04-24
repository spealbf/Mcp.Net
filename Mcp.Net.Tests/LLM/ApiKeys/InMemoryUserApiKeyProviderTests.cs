using Mcp.Net.LLM.ApiKeys;
using Mcp.Net.LLM.Interfaces;
using Mcp.Net.LLM.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Mcp.Net.Tests.LLM.ApiKeys;

public class InMemoryUserApiKeyProviderTests
{
    private readonly Mock<IApiKeyProvider> _mockFallbackProvider;
    private readonly Mock<ILogger<InMemoryUserApiKeyProvider>> _mockLogger;
    private readonly InMemoryUserApiKeyProvider _provider;

    public InMemoryUserApiKeyProviderTests()
    {
        _mockFallbackProvider = new Mock<IApiKeyProvider>();
        _mockLogger = new Mock<ILogger<InMemoryUserApiKeyProvider>>();
        _provider = new InMemoryUserApiKeyProvider(
            _mockLogger.Object,
            _mockFallbackProvider.Object
        );

        // Set up fallback provider behavior
        _mockFallbackProvider
            .Setup(p => p.GetApiKeyAsync(LlmProvider.OpenAI))
            .ReturnsAsync("default-openai-key");
        _mockFallbackProvider
            .Setup(p => p.GetApiKeyAsync(LlmProvider.Anthropic))
            .ReturnsAsync("default-anthropic-key");
    }

    [Fact]
    public async Task GetApiKeyForUserAsync_WithStoredKey_ReturnsUserKey()
    {
        // Arrange
        var userId = "test-user-1";
        var provider = LlmProvider.OpenAI;
        var expectedKey = "user1-openai-key";

        await _provider.SetApiKeyForUserAsync(provider, userId, expectedKey);

        // Act
        var result = await _provider.GetApiKeyForUserAsync(provider, userId);

        // Assert
        Assert.Equal(expectedKey, result);

        // Verify fallback provider was not called
        _mockFallbackProvider.Verify(p => p.GetApiKeyAsync(It.IsAny<LlmProvider>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task SetApiKeyForUserAsync_WithInvalidApiKey_ThrowsArgumentException(string? apiKey)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _provider.SetApiKeyForUserAsync(LlmProvider.OpenAI, "valid-user", apiKey!)
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task HasApiKeyForUserAsync_WithInvalidUserId_ThrowsArgumentException(string? userId)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _provider.HasApiKeyForUserAsync(LlmProvider.OpenAI, userId!)
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task DeleteApiKeyForUserAsync_WithInvalidUserId_ThrowsArgumentException(string? userId)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _provider.DeleteApiKeyForUserAsync(LlmProvider.OpenAI, userId!)
        );
    }

    [Fact]
    public async Task GetApiKeyForUserAsync_WithoutStoredKey_ReturnsFallbackKey()
    {
        // Arrange
        var userId = "test-user-2";
        var provider = LlmProvider.OpenAI;

        // Act
        var result = await _provider.GetApiKeyForUserAsync(provider, userId);

        // Assert
        Assert.Equal("default-openai-key", result);

        // Verify fallback provider was called
        _mockFallbackProvider.Verify(p => p.GetApiKeyAsync(provider), Times.Once);
    }

    [Fact]
    public async Task GetApiKeyForUserAsync_WhenFallbackProviderThrows_PropagatesException()
    {
        // Arrange
        var userId = "test-user-8";
        var provider = LlmProvider.Anthropic;

        _mockFallbackProvider
            .Setup(p => p.GetApiKeyAsync(provider))
            .ThrowsAsync(new KeyNotFoundException());

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _provider.GetApiKeyForUserAsync(provider, userId)
        );

        // Verify fallback provider was called
        _mockFallbackProvider.Verify(p => p.GetApiKeyAsync(provider), Times.Once);
    }

    [Fact]
    public async Task SetApiKeyForUserAsync_WithExistingKey_UpdatesKey()
    {
        // Arrange
        var userId = "test-user-9";
        var provider = LlmProvider.OpenAI;
        var initialKey = "initial-key";
        var updatedKey = "updated-key";

        await _provider.SetApiKeyForUserAsync(provider, userId, initialKey);
        Assert.Equal(initialKey, await _provider.GetApiKeyForUserAsync(provider, userId));

        // Act
        var result = await _provider.SetApiKeyForUserAsync(provider, userId, updatedKey);

        // Assert
        Assert.True(result);
        Assert.Equal(updatedKey, await _provider.GetApiKeyForUserAsync(provider, userId));
    }

    [Theory]
    [InlineData(LlmProvider.OpenAI)]
    [InlineData(LlmProvider.Anthropic)]
    public async Task SetAndGetApiKeyForUserAsync_WorksWithDifferentProviders(LlmProvider provider)
    {
        // Arrange
        var userId = "test-user-10";
        var expectedKey = $"user10-{provider.ToString().ToLower()}-key";

        // Act
        await _provider.SetApiKeyForUserAsync(provider, userId, expectedKey);
        var result = await _provider.GetApiKeyForUserAsync(provider, userId);

        // Assert
        Assert.Equal(expectedKey, result);
    }

    [Fact]
    public async Task SetApiKeyForUserAsync_SetsKey()
    {
        // Arrange
        var userId = "test-user-3";
        var provider = LlmProvider.Anthropic;
        var expectedKey = "user3-anthropic-key";

        // Act
        var result = await _provider.SetApiKeyForUserAsync(provider, userId, expectedKey);

        // Assert
        Assert.True(result);
        Assert.True(await _provider.HasApiKeyForUserAsync(provider, userId));
        Assert.Equal(expectedKey, await _provider.GetApiKeyForUserAsync(provider, userId));
    }

    [Fact]
    public async Task DeleteApiKeyForUserAsync_RemovesKey()
    {
        // Arrange
        var userId = "test-user-4";
        var provider = LlmProvider.OpenAI;
        var key = "user4-openai-key";

        await _provider.SetApiKeyForUserAsync(provider, userId, key);
        Assert.True(await _provider.HasApiKeyForUserAsync(provider, userId));

        // Act
        var result = await _provider.DeleteApiKeyForUserAsync(provider, userId);

        // Assert
        Assert.True(result);
        Assert.False(await _provider.HasApiKeyForUserAsync(provider, userId));
    }

    [Fact]
    public async Task HasApiKeyForUserAsync_ReturnsTrueForExistingKey()
    {
        // Arrange
        var userId = "test-user-5";
        var provider = LlmProvider.OpenAI;
        var key = "user5-openai-key";

        await _provider.SetApiKeyForUserAsync(provider, userId, key);

        // Act
        var result = await _provider.HasApiKeyForUserAsync(provider, userId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task HasApiKeyForUserAsync_ReturnsFalseForNonExistentKey()
    {
        // Arrange
        var userId = "test-user-6";
        var provider = LlmProvider.OpenAI;

        // Act
        var result = await _provider.HasApiKeyForUserAsync(provider, userId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteApiKeyForUserAsync_ReturnsFalseForNonExistentKey()
    {
        // Arrange
        var userId = "test-user-7";
        var provider = LlmProvider.OpenAI;

        // Act
        var result = await _provider.DeleteApiKeyForUserAsync(provider, userId);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task GetApiKeyForUserAsync_WithInvalidUserId_ThrowsArgumentException(
        string? userId
    )
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _provider.GetApiKeyForUserAsync(LlmProvider.OpenAI, userId!)
        );
    }
}
