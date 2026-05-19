using ElBruno.LocalEmbeddings.Azure.Options;
using Microsoft.Extensions.AI;
using Moq;
using OpenAI;
using Xunit;

namespace ElBruno.LocalEmbeddings.Azure.Tests;

/// <summary>
/// Tests for the HybridAzureEmbeddingGenerator fallback behavior.
/// </summary>
public class HybridAzureEmbeddingGeneratorTests
{
    private static readonly Embedding<float> SampleEmbedding = new(new float[] { 0.1f, 0.2f, 0.3f });

    [Fact]
    public async Task GenerateAsync_WithLocalSuccess_ReturnsLocalEmbeddings()
    {
        // Arrange
        var testInput = new[] { "test string" };
        var expectedEmbeddings = new[] { SampleEmbedding };
        var expectedResult = new GeneratedEmbeddings<Embedding<float>>(expectedEmbeddings);

        var localGeneratorMock = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        localGeneratorMock
            .Setup(g => g.GenerateAsync(
                It.IsAny<IEnumerable<string>>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var azureClientMock = new Mock<OpenAIClient>();
        var options = new LocalEmbeddingsAzureOptions
        {
            Endpoint = "https://test.openai.azure.com",
            ApiKey = "test-key",
            DeploymentName = "test-deploy"
        };

        var generator = new HybridAzureEmbeddingGenerator(
            localGeneratorMock.Object,
            azureClientMock.Object,
            options);

        // Act
        var result = await generator.GenerateAsync(testInput);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(SampleEmbedding.Vector, result[0].Vector);
        localGeneratorMock.Verify(
            g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        var localGeneratorMock = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        var azureClientMock = new Mock<OpenAIClient>();
        var options = new LocalEmbeddingsAzureOptions
        {
            Endpoint = "https://test.openai.azure.com",
            ApiKey = "test-key",
            DeploymentName = "test-deploy"
        };

        var generator = new HybridAzureEmbeddingGenerator(
            localGeneratorMock.Object,
            azureClientMock.Object,
            options);

        await generator.DisposeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => generator.GenerateAsync(new[] { "test" }));
    }

    [Fact]
    public void LocalEmbeddingsAzureOptions_Validate_FailsOnMissingEndpoint()
    {
        // Arrange
        var options = new LocalEmbeddingsAzureOptions
        {
            ApiKey = "test-key",
            DeploymentName = "test-deploy"
        };

        // Act
        var errors = options.Validate();

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains("Endpoint is required.", errors);
    }

    [Fact]
    public void LocalEmbeddingsAzureOptions_Validate_FailsOnMissingApiKey()
    {
        // Arrange
        var options = new LocalEmbeddingsAzureOptions
        {
            Endpoint = "https://test.openai.azure.com",
            DeploymentName = "test-deploy"
        };

        // Act
        var errors = options.Validate();

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains("ApiKey is required.", errors);
    }

    [Fact]
    public void LocalEmbeddingsAzureOptions_Validate_FailsOnMissingDeploymentName()
    {
        // Arrange
        var options = new LocalEmbeddingsAzureOptions
        {
            Endpoint = "https://test.openai.azure.com",
            ApiKey = "test-key"
        };

        // Act
        var errors = options.Validate();

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains("DeploymentName is required.", errors);
    }

    [Fact]
    public void LocalEmbeddingsAzureOptions_Validate_FailsOnInvalidTimeout()
    {
        // Arrange
        var options = new LocalEmbeddingsAzureOptions
        {
            Endpoint = "https://test.openai.azure.com",
            ApiKey = "test-key",
            DeploymentName = "test-deploy",
            TimeoutMilliseconds = 500
        };

        // Act
        var errors = options.Validate();

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains("TimeoutMilliseconds must be at least 1000.", errors);
    }

    [Fact]
    public void LocalEmbeddingsAzureOptions_Validate_Succeeds()
    {
        // Arrange
        var options = new LocalEmbeddingsAzureOptions
        {
            Endpoint = "https://test.openai.azure.com",
            ApiKey = "test-key",
            DeploymentName = "test-deploy",
            MaxFallbackAttempts = 3,
            TimeoutMilliseconds = 30_000
        };

        // Act
        var errors = options.Validate();

        // Assert
        Assert.Empty(errors);
    }
}
