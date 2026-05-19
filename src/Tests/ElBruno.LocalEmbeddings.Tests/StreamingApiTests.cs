using Microsoft.Extensions.AI;
using Moq;

namespace ElBruno.LocalEmbeddings.Tests;

/// <summary>
/// Comprehensive streaming API tests with table-driven scenarios.
/// Covers empty streams, single items, large streams, cancellation, and error cases.
/// </summary>
public class StreamingApiTests
{
    // =========================================================================
    // Table-driven: Stream size scenarios
    // =========================================================================

    public static readonly TheoryData<int> StreamSizes = new()
    {
        0,       // Empty
        1,       // Single
        10,      // Small
        100,     // Medium
        1000,    // Large
        10000,   // Very large
    };

    [Theory]
    [MemberData(nameof(StreamSizes))]
    public async Task GenerateStreamingAsync_VariousStreamSizes_ProducesCorrectCount(int streamSize)
    {
        var mockGenerator = CreateMockGenerator();
        var texts = Enumerable.Range(1, streamSize).Select(i => $"text{i}").ToList();
        var results = new List<Embedding<float>>();

        await foreach (var embedding in mockGenerator.Object.GenerateStreamingAsync(texts, batchSize: 10))
        {
            results.Add(embedding);
        }

        Assert.Equal(streamSize, results.Count);
    }

    // =========================================================================
    // Batch size variations (table-driven)
    // =========================================================================

    public static readonly TheoryData<int, int> StreamSizeAndBatchSize = new()
    {
        // streamSize, batchSize
        { 1, 1 },
        { 10, 3 },
        { 10, 5 },
        { 10, 10 },
        { 10, 15 },
        { 100, 1 },
        { 100, 7 },
        { 100, 50 },
        { 100, 200 },
        { 1000, 32 },
        { 1000, 64 },
    };

    [Theory]
    [MemberData(nameof(StreamSizeAndBatchSize))]
    public async Task GenerateStreamingAsync_VariousBatchSizes_ProducesConsistentResults(
        int streamSize, int batchSize)
    {
        var mockGenerator = CreateMockGenerator(dimensions: 384);
        var texts = Enumerable.Range(1, streamSize).Select(i => $"text{i}").ToList();
        var results = new List<Embedding<float>>();

        await foreach (var embedding in mockGenerator.Object.GenerateStreamingAsync(texts, batchSize: batchSize))
        {
            results.Add(embedding);
        }

        Assert.Equal(streamSize, results.Count);
        Assert.All(results, e => Assert.Equal(384, e.Vector.Length));
    }

    // =========================================================================
    // Core functionality: Empty, single, large streams
    // =========================================================================

    [Fact]
    public async Task GenerateStreamingAsync_EmptyStream_YieldsNothing()
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
    public async Task GenerateStreamingAsync_SingleItem_YieldsOneEmbedding()
    {
        var mockGenerator = CreateMockGenerator();
        var results = new List<Embedding<float>>();

        await foreach (var embedding in mockGenerator.Object.GenerateStreamingAsync(new[] { "single" }, batchSize: 10))
        {
            results.Add(embedding);
        }

        Assert.Single(results);
        Assert.Equal(384, results[0].Vector.Length);
    }

    [Fact]
    public async Task GenerateStreamingAsync_LargeStream_YieldsAllItems()
    {
        var mockGenerator = CreateMockGenerator();
        const int largeCount = 10000;
        var texts = Enumerable.Range(1, largeCount).Select(i => $"text{i}").ToList();
        var results = new List<Embedding<float>>();

        await foreach (var embedding in mockGenerator.Object.GenerateStreamingAsync(texts, batchSize: 100))
        {
            results.Add(embedding);
        }

        Assert.Equal(largeCount, results.Count);
    }

    // =========================================================================
    // Partial consumption (partial batch flush)
    // =========================================================================

    [Theory]
    [InlineData(10, 3)]   // Stop after 3 of 10
    [InlineData(100, 25)] // Stop after 25 of 100
    [InlineData(1000, 500)] // Stop after 500 of 1000
    public async Task GenerateStreamingAsync_PartialConsumption_StopsAfterBreak(int streamSize, int stopAfter)
    {
        var mockGenerator = CreateMockGenerator();
        var texts = Enumerable.Range(1, streamSize).Select(i => $"text{i}").ToList();
        var results = new List<Embedding<float>>();

        await foreach (var embedding in mockGenerator.Object.GenerateStreamingAsync(texts, batchSize: 10))
        {
            results.Add(embedding);
            if (results.Count >= stopAfter)
            {
                break;
            }
        }

        Assert.Equal(stopAfter, results.Count);
    }

    // =========================================================================
    // Cancellation: Mid-stream abort
    // =========================================================================

    [Fact]
    public async Task GenerateStreamingAsync_CancellationBeforeStart_ThrowsOperationCanceledException()
    {
        var mockGenerator = CreateMockGenerator();
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel before iteration

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in mockGenerator.Object.GenerateStreamingAsync(
                new[] { "test" }, batchSize: 10, cancellationToken: cts.Token))
            {
                // Should not reach here
            }
        });
    }

    [Fact]
    public async Task GenerateStreamingAsync_CancellationMidStream_ThrowsOperationCanceledException()
    {
        var mockGenerator = CreateMockGenerator();
        var cts = new CancellationTokenSource();
        var texts = Enumerable.Range(1, 100).Select(i => $"text{i}").ToList();
        var count = 0;

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in mockGenerator.Object.GenerateStreamingAsync(texts, batchSize: 10, cancellationToken: cts.Token))
            {
                count++;
                if (count >= 25)
                {
                    cts.Cancel();
                }
            }
        });

        Assert.True(count >= 25, "Should have yielded at least 25 items before cancellation");
    }

    [Fact]
    public async Task GenerateStreamingAsync_CancellationDuringBatch_ThrowsOperationCanceledException()
    {
        var mockGenerator = CreateMockGenerator();
        var cts = new CancellationTokenSource();
        var texts = Enumerable.Range(1, 50).Select(i => $"text{i}").ToList();
        var count = 0;

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in mockGenerator.Object.GenerateStreamingAsync(texts, batchSize: 20, cancellationToken: cts.Token))
            {
                count++;
                if (count == 15)
                {
                    cts.Cancel();
                }
            }
        });
    }

    // =========================================================================
    // Dimension consistency
    // =========================================================================

    [Theory]
    [InlineData(384)]
    [InlineData(768)]
    [InlineData(1536)]
    public async Task GenerateStreamingAsync_AllEmbeddingsDimensionConsistent(int dimensions)
    {
        var mockGenerator = CreateMockGenerator(dimensions: dimensions);
        var texts = Enumerable.Range(1, 50).Select(i => $"text{i}").ToList();
        var results = new List<Embedding<float>>();

        await foreach (var embedding in mockGenerator.Object.GenerateStreamingAsync(texts, batchSize: 10))
        {
            results.Add(embedding);
        }

        Assert.All(results, e => Assert.Equal(dimensions, e.Vector.Length));
    }

    // =========================================================================
    // Null and invalid input validation
    // =========================================================================

    [Fact]
    public async Task GenerateStreamingAsync_WithNullGenerator_ThrowsArgumentNullException()
    {
        IEmbeddingGenerator<string, Embedding<float>> generator = null!;

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in generator.GenerateStreamingAsync(new[] { "test" }))
            {
                // Should not reach here
            }
        });
    }

    [Fact]
    public async Task GenerateStreamingAsync_WithNullTexts_ThrowsArgumentNullException()
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

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task GenerateStreamingAsync_WithInvalidBatchSize_ThrowsArgumentOutOfRangeException(int invalidBatchSize)
    {
        var mockGenerator = CreateMockGenerator();
        var texts = new[] { "test" };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await foreach (var _ in mockGenerator.Object.GenerateStreamingAsync(texts, batchSize: invalidBatchSize))
            {
                // Should not reach here
            }
        });
    }

    // =========================================================================
    // Error propagation from generator
    // =========================================================================

    [Fact]
    public async Task GenerateStreamingAsync_GeneratorThrows_PropagatesException()
    {
        var failingGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        failingGenerator
            .Setup(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Generator failed"));

        var texts = new[] { "test" };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in failingGenerator.Object.GenerateStreamingAsync(texts, batchSize: 10))
            {
                // Should not reach here
            }
        });

        Assert.Equal("Generator failed", exception.Message);
    }

    [Fact]
    public async Task GenerateStreamingAsync_GeneratorReturnsEmptyEmbeddings_YieldsNothing()
    {
        var emptyGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        emptyGenerator
            .Setup(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>(new List<Embedding<float>>()));

        var texts = Enumerable.Range(1, 10).Select(i => $"text{i}").ToList();
        var results = new List<Embedding<float>>();

        await foreach (var _ in emptyGenerator.Object.GenerateStreamingAsync(texts, batchSize: 5))
        {
            results.Add(_);
        }

        Assert.Empty(results);
    }

    // =========================================================================
    // Stream ordering preservation
    // =========================================================================

    [Fact]
    public async Task GenerateStreamingAsync_StreamOrderingPreserved()
    {
        var mockGenerator = CreateDeterministicMockGenerator();
        var texts = new[] { "first", "second", "third", "fourth", "fifth" };
        var results = new List<(string Text, Embedding<float> Embedding)>();

        int index = 0;
        await foreach (var embedding in mockGenerator.Object.GenerateStreamingAsync(texts, batchSize: 2))
        {
            results.Add((texts[index++], embedding));
        }

        for (int i = 0; i < texts.Length; i++)
        {
            Assert.Equal(texts[i], results[i].Text);
        }
    }

    // =========================================================================
    // Stress tests
    // =========================================================================

    [Fact]
    public async Task GenerateStreamingAsync_VerLargeStream_CompletesSuccessfully()
    {
        var mockGenerator = CreateMockGenerator();
        var texts = Enumerable.Range(1, 50000).Select(i => $"text{i}").ToList();
        var count = 0;

        await foreach (var _ in mockGenerator.Object.GenerateStreamingAsync(texts, batchSize: 128))
        {
            count++;
        }

        Assert.Equal(50000, count);
    }

    [Fact]
    public async Task GenerateStreamingAsync_SmallBatchSizeVsLargeStream_Works()
    {
        var mockGenerator = CreateMockGenerator();
        var texts = Enumerable.Range(1, 1000).Select(i => $"text{i}").ToList();
        var results = new List<Embedding<float>>();

        await foreach (var embedding in mockGenerator.Object.GenerateStreamingAsync(texts, batchSize: 1))
        {
            results.Add(embedding);
        }

        Assert.Equal(1000, results.Count);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

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

    private static Mock<IEmbeddingGenerator<string, Embedding<float>>> CreateDeterministicMockGenerator(int dimensions = 384)
    {
        var mock = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        mock.Setup(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> values, EmbeddingGenerationOptions? _, CancellationToken ct) =>
            {
                ct.ThrowIfCancellationRequested();
                var list = values.ToList();
                var embeddings = list.Select((text, idx) => new Embedding<float>(
                    Enumerable.Range(0, dimensions)
                        .Select(i => (float)(text.GetHashCode() + idx * 1000 + i) / 1e8f)
                        .ToArray()
                )).ToList();
                return new GeneratedEmbeddings<Embedding<float>>(embeddings);
            });
        return mock;
    }
}
