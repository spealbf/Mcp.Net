using Mcp.Net.LLM.ApiKeys;
using Mcp.Net.LLM.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace Mcp.Net.Tests.LLM.ApiKeys;

public class DefaultApiKeyProviderTests
{
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<DefaultApiKeyProvider>> _mockLogger;
    private readonly DefaultApiKeyProvider _provider;

    public DefaultApiKeyProviderTests()
    {
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<DefaultApiKeyProvider>>();
        _provider = new DefaultApiKeyProvider(_mockConfiguration.Object, _mockLogger.Object);

        // Set up environment variables for testing
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-openai-key-from-env");
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "test-anthropic-key-from-env");
    }

    [Fact]
    public async Task GetApiKeyAsync_WithConfigurationValue_ReturnsConfigKey()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["OpenAI:ApiKey"]).Returns("test-openai-key-from-config");

        // Act
        var result = await _provider.GetApiKeyAsync(LlmProvider.OpenAI);

        // Assert
        Assert.Equal("test-openai-key-from-config", result);
    }

    [Fact]
    public async Task GetApiKeyAsync_WithoutConfigurationValue_ReturnsEnvironmentKey()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["OpenAI:ApiKey"]).Returns((string?)null);

        // Act
        var result = await _provider.GetApiKeyAsync(LlmProvider.OpenAI);

        // Assert
        Assert.Equal("test-openai-key-from-env", result);
    }

    [Fact]
    public async Task GetApiKeyAsync_WithoutAnyKey_ThrowsKeyNotFoundException()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["OpenAI:ApiKey"]).Returns((string?)null);
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _provider.GetApiKeyAsync(LlmProvider.OpenAI)
        );
    }

    [Fact]
    public async Task GetApiKeyAsync_WithInvalidProvider_ThrowsArgumentOutOfRangeException()
    {
        // Arrange - create an invalid provider value
        var invalidProvider = (LlmProvider)999;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _provider.GetApiKeyAsync(invalidProvider)
        );
    }

    [Fact]
    public async Task GetApiKeyAsync_ForOpenAI_ReturnsOpenAIKey()
    {
        // Act
        var result = await _provider.GetApiKeyAsync(LlmProvider.OpenAI);

        // Assert
        Assert.Contains("openai", result.ToLower());
    }

    [Fact]
    public async Task GetApiKeyAsync_ForAnthropic_ReturnsAnthropicKey()
    {
        // Act
        var result = await _provider.GetApiKeyAsync(LlmProvider.Anthropic);

        // Assert
        Assert.Contains("anthropic", result.ToLower());
    }
}
