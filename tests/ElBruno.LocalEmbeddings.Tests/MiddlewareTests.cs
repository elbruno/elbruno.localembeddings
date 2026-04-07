using System.Diagnostics;
using ElBruno.LocalEmbeddings.Middleware;
using Microsoft.Extensions.AI;
using Moq;

namespace ElBruno.LocalEmbeddings.Tests;

public class MiddlewareTests
{
    [Fact]
    public async Task OpenTelemetryMiddleware_CallsInnerGenerator()
    {
        var mockInner = CreateMockGenerator();
        var middleware = new OpenTelemetryEmbeddingMiddleware(mockInner.Object, "test-model");
        var texts = new[] { "test" };

        var result = await middleware.GenerateAsync(texts);

        Assert.NotNull(result);
        Assert.Single(result);
        mockInner.Verify(
            g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OpenTelemetryMiddleware_ReturnsResults()
    {
        var mockInner = CreateMockGenerator();
        var middleware = new OpenTelemetryEmbeddingMiddleware(mockInner.Object, "test-model");
        var texts = new[] { "apple", "banana" };

        var result = await middleware.GenerateAsync(texts);

        Assert.Equal(2, result.Count);
        Assert.All(result, embedding => Assert.Equal(384, embedding.Vector.Length));
    }

    [Fact]
    public async Task OpenTelemetryMiddleware_UsesMetadataWhenModelNameNull()
    {
        var mockInner = CreateMockGenerator();
        var metadata = new EmbeddingGeneratorMetadata("metadata-model");
        mockInner.Setup(g => g.GetService(typeof(EmbeddingGeneratorMetadata), null))
            .Returns(metadata);

        var middleware = new OpenTelemetryEmbeddingMiddleware(mockInner.Object, modelName: null);

        Assert.NotNull(middleware);
    }

    [Fact]
    public async Task RetryMiddleware_SucceedsOnFirstTry()
    {
        var mockInner = CreateMockGenerator();
        var middleware = new RetryEmbeddingMiddleware(mockInner.Object, maxRetries: 3);
        var texts = new[] { "test" };

        var result = await middleware.GenerateAsync(texts);

        Assert.NotNull(result);
        mockInner.Verify(
            g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RetryMiddleware_RetriesOnTransientException()
    {
        var mockInner = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        var attempts = 0;
        mockInner.Setup(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> values, EmbeddingGenerationOptions? _, CancellationToken _) =>
            {
                attempts++;
                if (attempts < 2)
                {
                    throw new IOException("Transient error");
                }
                var list = values.ToList();
                return new GeneratedEmbeddings<Embedding<float>>(
                    list.Select(_ => new Embedding<float>(new float[384])).ToList()
                );
            });

        var middleware = new RetryEmbeddingMiddleware(mockInner.Object, maxRetries: 3);
        var result = await middleware.GenerateAsync(new[] { "test" });

        Assert.NotNull(result);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task RetryMiddleware_GivesUpAfterMaxRetries()
    {
        var mockInner = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        mockInner.Setup(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Persistent error"));

        var middleware = new RetryEmbeddingMiddleware(mockInner.Object, maxRetries: 2);

        await Assert.ThrowsAsync<IOException>(async () =>
            await middleware.GenerateAsync(new[] { "test" }));
    }

    [Fact]
    public async Task RetryMiddleware_RetriesOnIOException()
    {
        var mockInner = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        var attempts = 0;
        mockInner.Setup(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> values, EmbeddingGenerationOptions? _, CancellationToken _) =>
            {
                attempts++;
                if (attempts < 2)
                {
                    throw new IOException("Transient IO error");
                }
                var list = values.ToList();
                return new GeneratedEmbeddings<Embedding<float>>(
                    list.Select(_ => new Embedding<float>(new float[384])).ToList()
                );
            });

        var middleware = new RetryEmbeddingMiddleware(mockInner.Object, maxRetries: 3);
        var result = await middleware.GenerateAsync(new[] { "test" });

        Assert.NotNull(result);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public void RetryMiddleware_InvalidMaxRetries_ThrowsArgumentOutOfRangeException()
    {
        var mockInner = CreateMockGenerator();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RetryEmbeddingMiddleware(mockInner.Object, maxRetries: 0));
    }

    [Fact]
    public void RetryMiddleware_NegativeMaxRetries_ThrowsArgumentOutOfRangeException()
    {
        var mockInner = CreateMockGenerator();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RetryEmbeddingMiddleware(mockInner.Object, maxRetries: -1));
    }

    [Fact]
    public void UseOpenTelemetry_ExtensionMethod_ReturnsMiddleware()
    {
        var mockInner = CreateMockGenerator();

        var result = mockInner.Object.UseOpenTelemetry("test-model");

        Assert.NotNull(result);
        Assert.IsType<OpenTelemetryEmbeddingMiddleware>(result);
    }

    [Fact]
    public void UseRetry_ExtensionMethod_ReturnsMiddleware()
    {
        var mockInner = CreateMockGenerator();

        var result = mockInner.Object.UseRetry(maxRetries: 5);

        Assert.NotNull(result);
        Assert.IsType<RetryEmbeddingMiddleware>(result);
    }

    [Fact]
    public void UseOpenTelemetry_WithNullGenerator_ThrowsArgumentNullException()
    {
        IEmbeddingGenerator<string, Embedding<float>> generator = null!;

        Assert.Throws<ArgumentNullException>(() => generator.UseOpenTelemetry());
    }

    [Fact]
    public void UseRetry_WithNullGenerator_ThrowsArgumentNullException()
    {
        IEmbeddingGenerator<string, Embedding<float>> generator = null!;

        Assert.Throws<ArgumentNullException>(() => generator.UseRetry());
    }

    [Fact]
    public async Task Middleware_CanBeChained()
    {
        var mockInner = CreateMockGenerator();

        var wrapped = mockInner.Object
            .UseRetry(maxRetries: 3)
            .UseOpenTelemetry("test-model");

        var result = await wrapped.GenerateAsync(new[] { "test" });

        Assert.NotNull(result);
        Assert.Single(result);
    }

    private static Mock<IEmbeddingGenerator<string, Embedding<float>>> CreateMockGenerator(int dimensions = 384)
    {
        var mock = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        mock.Setup(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> values, EmbeddingGenerationOptions? _, CancellationToken _) =>
            {
                var list = values.ToList();
                var embeddings = list.Select(_ => new Embedding<float>(
                    Enumerable.Range(0, dimensions).Select(i => (float)Random.Shared.NextDouble()).ToArray()
                )).ToList();
                return new GeneratedEmbeddings<Embedding<float>>(embeddings);
            });

        mock.Setup(g => g.GetService(typeof(EmbeddingGeneratorMetadata), null))
            .Returns(new EmbeddingGeneratorMetadata("mock-model"));

        return mock;
    }
}
