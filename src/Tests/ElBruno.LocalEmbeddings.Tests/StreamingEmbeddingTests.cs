using Microsoft.Extensions.AI;
using Moq;

namespace ElBruno.LocalEmbeddings.Tests;

public class StreamingEmbeddingTests
{
    [Fact]
    public async Task GenerateStreamingAsync_ReturnsCorrectNumberOfEmbeddings()
    {
        var mockGenerator = CreateMockGenerator();
        var texts = Enumerable.Range(1, 30).Select(i => $"text{i}").ToList();
        var results = new List<Embedding<float>>();

        await foreach (var embedding in mockGenerator.Object.GenerateStreamingAsync(texts, batchSize: 10))
        {
            results.Add(embedding);
        }

        Assert.Equal(30, results.Count);
    }

    [Fact]
    public async Task GenerateStreamingAsync_EachEmbeddingHasCorrectDimensions()
    {
        var mockGenerator = CreateMockGenerator(dimensions: 768);
        var texts = new[] { "apple", "banana", "cherry" };
        var results = new List<Embedding<float>>();

        await foreach (var embedding in mockGenerator.Object.GenerateStreamingAsync(texts, batchSize: 2))
        {
            results.Add(embedding);
        }

        Assert.All(results, embedding => Assert.Equal(768, embedding.Vector.Length));
    }

    [Fact]
    public async Task GenerateStreamingAsync_EmptyInputYieldsNothing()
    {
        var mockGenerator = CreateMockGenerator();
        var results = new List<Embedding<float>>();

        await foreach (var embedding in mockGenerator.Object.GenerateStreamingAsync(Array.Empty<string>(), batchSize: 10))
        {
            results.Add(embedding);
        }

        Assert.Empty(results);
    }

    [Fact]
    public async Task GenerateStreamingAsync_CancellationTokenStopsEnumeration()
    {
        var mockGenerator = CreateMockGenerator();
        var texts = Enumerable.Range(1, 100).Select(i => $"text{i}").ToList();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var embedding in mockGenerator.Object.GenerateStreamingAsync(texts, batchSize: 10, cancellationToken: cts.Token))
            {
                // Should not reach here
            }
        });
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public async Task GenerateStreamingAsync_DifferentBatchSizesProduceSameTotalResults(int batchSize)
    {
        var mockGenerator = CreateMockGenerator();
        var texts = Enumerable.Range(1, 25).Select(i => $"text{i}").ToList();
        var results = new List<Embedding<float>>();

        await foreach (var embedding in mockGenerator.Object.GenerateStreamingAsync(texts, batchSize: batchSize))
        {
            results.Add(embedding);
        }

        Assert.Equal(25, results.Count);
    }

    [Fact]
    public async Task GenerateStreamingAsync_CanConsumePartialResults()
    {
        var mockGenerator = CreateMockGenerator();
        var texts = Enumerable.Range(1, 100).Select(i => $"text{i}").ToList();
        var results = new List<Embedding<float>>();

        await foreach (var embedding in mockGenerator.Object.GenerateStreamingAsync(texts, batchSize: 10))
        {
            results.Add(embedding);
            if (results.Count >= 15)
            {
                break;
            }
        }

        Assert.Equal(15, results.Count);
    }

    [Fact]
    public async Task GenerateStreamingAsync_WithNullGenerator_ThrowsArgumentNullException()
    {
        IEmbeddingGenerator<string, Embedding<float>> generator = null!;
        var texts = new[] { "test" };

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in generator.GenerateStreamingAsync(texts))
            {
                // Should not reach here
            }
        });
    }

    [Fact]
    public async Task GenerateStreamingAsync_WithNullValues_ThrowsArgumentNullException()
    {
        var mockGenerator = CreateMockGenerator();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in mockGenerator.Object.GenerateStreamingAsync(null!, batchSize: 10))
            {
                // Should not reach here
            }
        });
    }

    [Fact]
    public async Task GenerateStreamingAsync_WithInvalidBatchSize_ThrowsArgumentOutOfRangeException()
    {
        var mockGenerator = CreateMockGenerator();
        var texts = new[] { "test" };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await foreach (var _ in mockGenerator.Object.GenerateStreamingAsync(texts, batchSize: 0))
            {
                // Should not reach here
            }
        });
    }

    [Fact]
    public async Task GenerateStreamingAsync_SingleItemWorks()
    {
        var mockGenerator = CreateMockGenerator();
        var texts = new[] { "single" };
        var results = new List<Embedding<float>>();

        await foreach (var embedding in mockGenerator.Object.GenerateStreamingAsync(texts, batchSize: 10))
        {
            results.Add(embedding);
        }

        Assert.Single(results);
        Assert.Equal(384, results[0].Vector.Length);
    }

    private static Mock<IEmbeddingGenerator<string, Embedding<float>>> CreateMockGenerator(int dimensions = 384)
    {
        var mock = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        mock.Setup(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> values, EmbeddingGenerationOptions? _, CancellationToken ct) =>
            {
                ct.ThrowIfCancellationRequested();
                var list = values.ToList();
                var embeddings = list.Select(_ => new Embedding<float>(
                    Enumerable.Range(0, dimensions).Select(i => (float)Random.Shared.NextDouble()).ToArray()
                )).ToList();
                return new GeneratedEmbeddings<Embedding<float>>(embeddings);
            });
        return mock;
    }
}
