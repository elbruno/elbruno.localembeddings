using Microsoft.Extensions.AI;
using Moq;

namespace ElBruno.LocalEmbeddings.Tests;

public class BatchEmbeddingTests
{
    [Fact]
    public async Task GenerateAsync_WithProgress_ReportsCorrectCounts()
    {
        var mockGenerator = CreateMockGenerator();
        var texts = Enumerable.Range(1, 50).Select(i => $"text{i}").ToList();
        var progressReports = new List<EmbeddingProgress>();
        var progress = new Progress<EmbeddingProgress>(p =>
        {
            lock (progressReports)
            {
                progressReports.Add(p);
            }
        });

        var result = await mockGenerator.Object.GenerateAsync(
            texts,
            progress,
            batchSize: 10);

        await Task.Delay(100);

        Assert.Equal(50, result.Count);
        Assert.True(progressReports.Count >= 5, $"Expected at least 5 progress reports, got {progressReports.Count}");
        
        if (progressReports.Count >= 5)
        {
            Assert.Equal(10, progressReports[0].CompletedItems);
            Assert.Equal(50, progressReports[0].TotalItems);
            Assert.Equal(10, progressReports[0].CurrentBatchSize);
        }
    }

    [Fact]
    public async Task GenerateAsync_WithProgress_AllItemsEmbedded()
    {
        var mockGenerator = CreateMockGenerator();
        var texts = new[] { "apple", "banana", "cherry", "date" };
        var progress = new Progress<EmbeddingProgress>();

        var result = await mockGenerator.Object.GenerateAsync(
            texts,
            progress,
            batchSize: 2);

        Assert.Equal(4, result.Count);
        Assert.All(result, embedding => Assert.Equal(384, embedding.Vector.Length));
    }

    [Fact]
    public async Task GenerateAsync_WithProgress_CancellationTokenRespected()
    {
        var mockGenerator = CreateMockGenerator();
        var texts = Enumerable.Range(1, 100).Select(i => $"text{i}");
        var progress = new Progress<EmbeddingProgress>();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await mockGenerator.Object.GenerateAsync(
                texts,
                progress,
                batchSize: 10,
                cancellationToken: cts.Token));
    }

    [Fact]
    public async Task GenerateAsync_WithProgress_EmptyInputReturnsEmpty()
    {
        var mockGenerator = CreateMockGenerator();
        var progress = new Progress<EmbeddingProgress>();

        var result = await mockGenerator.Object.GenerateAsync(
            Array.Empty<string>(),
            progress,
            batchSize: 10);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GenerateAsync_WithProgress_SingleItemWorks()
    {
        var mockGenerator = CreateMockGenerator();
        var progressReports = new List<EmbeddingProgress>();
        var progress = new Progress<EmbeddingProgress>(p =>
        {
            lock (progressReports)
            {
                progressReports.Add(p);
            }
        });

        var result = await mockGenerator.Object.GenerateAsync(
            new[] { "single" },
            progress,
            batchSize: 10);

        await Task.Delay(100);

        Assert.Single(result);
        Assert.True(progressReports.Count >= 1, $"Expected at least 1 progress report, got {progressReports.Count}");
        
        if (progressReports.Count >= 1)
        {
            Assert.Equal(1, progressReports[0].CompletedItems);
            Assert.Equal(1, progressReports[0].TotalItems);
            Assert.Equal(1, progressReports[0].CurrentBatchSize);
        }
    }

    [Fact]
    public async Task GenerateAsync_WithProgress_LargeBatchReportsCorrectly()
    {
        var mockGenerator = CreateMockGenerator();
        var texts = Enumerable.Range(1, 150).Select(i => $"text{i}").ToList();
        var progressReports = new List<EmbeddingProgress>();
        var progress = new Progress<EmbeddingProgress>(p =>
        {
            lock (progressReports)
            {
                progressReports.Add(p);
            }
        });

        var result = await mockGenerator.Object.GenerateAsync(
            texts,
            progress,
            batchSize: 25);

        await Task.Delay(100);

        Assert.Equal(150, result.Count);
        Assert.True(progressReports.Count >= 6, $"Expected at least 6 progress reports, got {progressReports.Count}");
        
        if (progressReports.Count > 0)
        {
            Assert.Equal(150, progressReports.Last().CompletedItems);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public async Task GenerateAsync_WithProgress_CustomBatchSizesWork(int batchSize)
    {
        var mockGenerator = CreateMockGenerator();
        var texts = Enumerable.Range(1, 20).Select(i => $"text{i}").ToList();
        var progressReports = new List<EmbeddingProgress>();
        var progress = new Progress<EmbeddingProgress>(p => progressReports.Add(p));

        var result = await mockGenerator.Object.GenerateAsync(
            texts,
            progress,
            batchSize: batchSize);

        await Task.Delay(100);

        Assert.Equal(20, result.Count);
        Assert.True(progressReports.Count > 0);
    }

    [Fact]
    public async Task GenerateAsync_WithNullProgress_ThrowsArgumentNullException()
    {
        var mockGenerator = CreateMockGenerator();
        var texts = new[] { "test" };

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await mockGenerator.Object.GenerateAsync(
                texts,
                null!,
                batchSize: 10));
    }

    [Fact]
    public async Task GenerateAsync_WithProgress_InvalidBatchSizeThrows()
    {
        var mockGenerator = CreateMockGenerator();
        var texts = new[] { "test" };
        var progress = new Progress<EmbeddingProgress>();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await mockGenerator.Object.GenerateAsync(
                texts,
                progress,
                batchSize: 0));
    }

    [Fact]
    public async Task GenerateAsync_WithProgress_NegativeBatchSizeThrows()
    {
        var mockGenerator = CreateMockGenerator();
        var texts = new[] { "test" };
        var progress = new Progress<EmbeddingProgress>();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await mockGenerator.Object.GenerateAsync(
                texts,
                progress,
                batchSize: -1));
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
