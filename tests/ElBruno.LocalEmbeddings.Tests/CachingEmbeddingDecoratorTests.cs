using Microsoft.Extensions.AI;
using Moq;

namespace ElBruno.LocalEmbeddings.Tests;

public class CachingEmbeddingDecoratorTests
{
    [Fact]
    public async Task GenerateAsync_CacheHit_CallsInnerGeneratorOnlyOnce()
    {
        var mockInner = CreateMockGenerator();
        var decorator = new CachingEmbeddingDecorator(mockInner.Object, maxSize: 100);
        var text = "test text";

        var result1 = await decorator.GenerateAsync([text]);
        var result2 = await decorator.GenerateAsync([text]);

        mockInner.Verify(
            g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.Equal(result1[0].Vector.ToArray(), result2[0].Vector.ToArray());
    }

    [Fact]
    public async Task GenerateAsync_CacheMiss_CallsInnerGenerator()
    {
        var mockInner = CreateMockGenerator();
        var decorator = new CachingEmbeddingDecorator(mockInner.Object, maxSize: 100);
        var text1 = "first text";
        var text2 = "second text";

        await decorator.GenerateAsync([text1]);
        await decorator.GenerateAsync([text2]);

        mockInner.Verify(
            g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task GenerateAsync_MaxSizeEviction_EvictsOldestEntries()
    {
        var mockInner = CreateMockGenerator();
        var decorator = new CachingEmbeddingDecorator(mockInner.Object, maxSize: 2);

        await decorator.GenerateAsync(["text1"]);
        await decorator.GenerateAsync(["text2"]);
        await decorator.GenerateAsync(["text3"]);
        
        mockInner.Invocations.Clear();

        await decorator.GenerateAsync(["text1"]);

        mockInner.Verify(
            g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "text1 should have been evicted and require regeneration");
    }

    [Fact]
    public async Task GenerateAsync_ConcurrentAccess_DoesNotCrash()
    {
        var mockInner = CreateMockGenerator();
        var decorator = new CachingEmbeddingDecorator(mockInner.Object, maxSize: 100);
        var texts = Enumerable.Range(1, 50).Select(i => $"text{i}").ToList();

        var tasks = texts.Select(text => Task.Run(async () =>
        {
            await decorator.GenerateAsync([text]);
            await decorator.GenerateAsync([text]);
        })).ToArray();

        await Task.WhenAll(tasks);

        Assert.True(true, "No crash occurred during concurrent access");
    }

    [Fact]
    public void Dispose_PropagatesToInnerGenerator()
    {
        var mockInner = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        mockInner.As<IDisposable>();
        var decorator = new CachingEmbeddingDecorator(mockInner.Object, maxSize: 100);

        decorator.Dispose();

        mockInner.As<IDisposable>().Verify(d => d.Dispose(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_PropagatesToInnerGenerator()
    {
        var mockInner = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        mockInner.As<IAsyncDisposable>();
        var decorator = new CachingEmbeddingDecorator(mockInner.Object, maxSize: 100);

        await decorator.DisposeAsync();

        mockInner.As<IAsyncDisposable>().Verify(d => d.DisposeAsync(), Times.Once);
    }

    [Fact]
    public void Constructor_WithDefaultMaxSize_Succeeds()
    {
        var mockInner = CreateMockGenerator();
        var decorator = new CachingEmbeddingDecorator(mockInner.Object);

        Assert.NotNull(decorator);
    }

    [Fact]
    public void GetService_DelegatesToInnerGenerator()
    {
        var mockInner = CreateMockGenerator();
        var metadata = new EmbeddingGeneratorMetadata("test-model");
        mockInner.Setup(g => g.GetService(typeof(EmbeddingGeneratorMetadata), null))
            .Returns(metadata);

        var decorator = new CachingEmbeddingDecorator(mockInner.Object, maxSize: 100);

        var result = decorator.GetService<EmbeddingGeneratorMetadata>();

        Assert.Equal(metadata, result);
    }

    [Fact]
    public void Constructor_WithNullInnerGenerator_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CachingEmbeddingDecorator(null!, maxSize: 100));
    }

    [Fact]
    public void Constructor_WithInvalidMaxSize_ThrowsArgumentOutOfRangeException()
    {
        var mockInner = CreateMockGenerator();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CachingEmbeddingDecorator(mockInner.Object, maxSize: 0));
    }

    [Fact]
    public async Task GenerateAsync_BatchWithMixedCacheHitsMisses_MergesCorrectly()
    {
        var mockInner = CreateMockGenerator();
        var decorator = new CachingEmbeddingDecorator(mockInner.Object, maxSize: 100);

        await decorator.GenerateAsync(["text1", "text2"]);
        
        mockInner.Invocations.Clear();

        var result = await decorator.GenerateAsync(["text1", "text3", "text2"]);

        Assert.Equal(3, result.Count);
        
        mockInner.Verify(
            g => g.GenerateAsync(It.Is<IEnumerable<string>>(v => v.Count() == 1 && v.First() == "text3"), 
                                 It.IsAny<EmbeddingGenerationOptions>(), 
                                 It.IsAny<CancellationToken>()),
            Times.Once,
            "Only text3 should have been sent to inner generator");
    }

    private static Mock<IEmbeddingGenerator<string, Embedding<float>>> CreateMockGenerator(int dimensions = 384)
    {
        var mock = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        var callCounter = 0;
        
        mock.Setup(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> values, EmbeddingGenerationOptions? _, CancellationToken _) =>
            {
                var list = values.ToList();
                var seed = ++callCounter;
                var embeddings = list.Select((text, idx) =>
                {
                    var random = new Random(text.GetHashCode() + seed);
                    return new Embedding<float>(
                        Enumerable.Range(0, dimensions).Select(i => (float)random.NextDouble()).ToArray()
                    );
                }).ToList();
                return new GeneratedEmbeddings<Embedding<float>>(embeddings);
            });

        mock.Setup(g => g.GetService(typeof(EmbeddingGeneratorMetadata), null))
            .Returns(new EmbeddingGeneratorMetadata("mock-model"));

        return mock;
    }
}
