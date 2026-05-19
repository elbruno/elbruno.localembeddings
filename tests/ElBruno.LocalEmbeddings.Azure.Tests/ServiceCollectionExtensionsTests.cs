using ElBruno.LocalEmbeddings.Azure.Extensions;
using ElBruno.LocalEmbeddings.Azure.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElBruno.LocalEmbeddings.Azure.Tests;

/// <summary>
/// Tests for service collection extension methods.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddLocalEmbeddingsWithAzureFallback_RegistersHybridGenerator()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockGenerator = new MockEmbeddingGenerator();
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(mockGenerator);

        // Act
        services.AddLocalEmbeddingsWithAzureFallback(options =>
        {
            options.Endpoint = "https://test.openai.azure.com";
            options.ApiKey = "test-key";
            options.DeploymentName = "test-deploy";
        });

        var provider = services.BuildServiceProvider();
        var generator = provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

        // Assert
        Assert.NotNull(generator);
        Assert.IsType<HybridAzureEmbeddingGenerator>(generator);
    }

    [Fact]
    public void AddLocalEmbeddingsWithAzureFallback_WithInvalidOptions_ThrowsInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockGenerator = new MockEmbeddingGenerator();
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(mockGenerator);

        services.AddLocalEmbeddingsWithAzureFallback(options =>
        {
            // Deliberately leave options empty (invalid)
        });

        var provider = services.BuildServiceProvider();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>());
    }

    [Fact]
    public void AddLocalEmbeddingsWithAzureFallback_AllowsChaining()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockGenerator = new MockEmbeddingGenerator();
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(mockGenerator);

        // Act
        var result = services.AddLocalEmbeddingsWithAzureFallback(options =>
        {
            options.Endpoint = "https://test.openai.azure.com";
            options.ApiKey = "test-key";
            options.DeploymentName = "test-deploy";
        });

        // Assert
        Assert.Same(services, result);
    }

    private sealed class MockEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>, IDisposable
    {
        public EmbeddingGeneratorMetadata Metadata => new("Mock");

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var embeddings = values.Select(v => new Embedding<float>(new float[] { 0.1f, 0.2f, 0.3f })).ToArray();
            return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(embeddings));
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
            // No resources to dispose
        }
    }
}
