using ElBruno.LocalEmbeddings.Extensions;
using Microsoft.Extensions.AI;
using Moq;

namespace ElBruno.LocalEmbeddings.Tests;

public class EmbeddingComparerTests
{
    [Fact]
    public async Task CompareAsync_WithTwoGenerators_ReturnsBothResults()
    {
        var gen1 = CreateMockGenerator("model1");
        var gen2 = CreateMockGenerator("model2");
        var comparer = new EmbeddingComparer([
            ("Model 1", gen1.Object),
            ("Model 2", gen2.Object)
        ]);
        var texts = new[] { "apple", "banana", "cherry" };

        var report = await comparer.CompareAsync(texts);

        Assert.Equal(2, report.Results.Count);
        Assert.Contains(report.Results, r => r.ModelName == "Model 1");
        Assert.Contains(report.Results, r => r.ModelName == "Model 2");
    }

    [Fact]
    public async Task CompareAsync_SimilarityScoresInValidRange()
    {
        var gen = CreateNormalizedMockGenerator("model");
        var comparer = new EmbeddingComparer([("Model", gen.Object)]);
        var texts = new[] { "apple", "banana", "cherry" };

        var report = await comparer.CompareAsync(texts);

        var result = report.Results[0];
        Assert.All(result.PairwiseSimilarities, sim =>
            Assert.InRange(sim, -1f, 1f));
        Assert.InRange(result.AverageSimilarity, -1f, 1f);
        Assert.InRange(result.MinSimilarity, -1f, 1f);
        Assert.InRange(result.MaxSimilarity, -1f, 1f);
    }

    [Fact]
    public async Task CompareAsync_EmptyTextList_ThrowsArgumentException()
    {
        var gen = CreateMockGenerator("model");
        var comparer = new EmbeddingComparer([("Model", gen.Object)]);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await comparer.CompareAsync(Array.Empty<string>()));
    }

    [Fact]
    public async Task CompareAsync_SingleText_ThrowsArgumentException()
    {
        var gen = CreateMockGenerator("model");
        var comparer = new EmbeddingComparer([("Model", gen.Object)]);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await comparer.CompareAsync(new[] { "single" }));
    }

    [Fact]
    public async Task CompareAsync_ReportContainsCorrectModelNames()
    {
        var gen1 = CreateMockGenerator("model1");
        var gen2 = CreateMockGenerator("model2");
        var comparer = new EmbeddingComparer([
            ("Custom Name 1", gen1.Object),
            ("Custom Name 2", gen2.Object)
        ]);
        var texts = new[] { "text1", "text2" };

        var report = await comparer.CompareAsync(texts);

        Assert.Equal("Custom Name 1", report.Results[0].ModelName);
        Assert.Equal("Custom Name 2", report.Results[1].ModelName);
    }

    [Fact]
    public async Task CompareAsync_PairwiseSimilarityCountCorrect()
    {
        var gen = CreateMockGenerator("model");
        var comparer = new EmbeddingComparer([("Model", gen.Object)]);
        var texts = new[] { "a", "b", "c", "d", "e" };

        var report = await comparer.CompareAsync(texts);

        var expectedPairs = 5 * 4 / 2;
        Assert.Equal(expectedPairs, report.Results[0].PairwiseSimilarities.Count);
    }

    [Fact]
    public async Task CompareAsync_WithTwoTexts_ReturnsOneSimilarity()
    {
        var gen = CreateMockGenerator("model");
        var comparer = new EmbeddingComparer([("Model", gen.Object)]);
        var texts = new[] { "first", "second" };

        var report = await comparer.CompareAsync(texts);

        Assert.Single(report.Results[0].PairwiseSimilarities);
    }

    [Fact]
    public async Task CompareAsync_MinMaxSimilarityCorrect()
    {
        var gen = CreateMockGenerator("model");
        var comparer = new EmbeddingComparer([("Model", gen.Object)]);
        var texts = new[] { "apple", "banana", "cherry" };

        var report = await comparer.CompareAsync(texts);

        var result = report.Results[0];
        Assert.Equal(result.PairwiseSimilarities.Min(), result.MinSimilarity);
        Assert.Equal(result.PairwiseSimilarities.Max(), result.MaxSimilarity);
    }

    [Fact]
    public async Task CompareAsync_AverageSimilarityCorrect()
    {
        var gen = CreateMockGenerator("model");
        var comparer = new EmbeddingComparer([("Model", gen.Object)]);
        var texts = new[] { "apple", "banana", "cherry" };

        var report = await comparer.CompareAsync(texts);

        var result = report.Results[0];
        var expectedAverage = result.PairwiseSimilarities.Average();
        Assert.Equal(expectedAverage, result.AverageSimilarity, precision: 5);
    }

    [Fact]
    public void Constructor_WithEmptyGenerators_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new EmbeddingComparer(Array.Empty<(string, IEmbeddingGenerator<string, Embedding<float>>)>()));
    }

    [Fact]
    public void Constructor_WithNullGenerators_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new EmbeddingComparer(null!));
    }

    [Fact]
    public async Task CompareAsync_WithNullTexts_ThrowsArgumentNullException()
    {
        var gen = CreateMockGenerator("model");
        var comparer = new EmbeddingComparer([("Model", gen.Object)]);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await comparer.CompareAsync(null!));
    }

    [Fact]
    public async Task CompareAsync_ReportTextsMatchInput()
    {
        var gen = CreateMockGenerator("model");
        var comparer = new EmbeddingComparer([("Model", gen.Object)]);
        var texts = new[] { "apple", "banana", "cherry" };

        var report = await comparer.CompareAsync(texts);

        Assert.Equal(texts, report.Texts);
    }

    private static Mock<IEmbeddingGenerator<string, Embedding<float>>> CreateMockGenerator(string modelName, int dimensions = 384)
    {
        var mock = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        
        mock.Setup(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> values, EmbeddingGenerationOptions? _, CancellationToken _) =>
            {
                var list = values.ToList();
                var embeddings = list.Select(text =>
                {
                    var random = new Random(text.GetHashCode() + modelName.GetHashCode());
                    return new Embedding<float>(
                        Enumerable.Range(0, dimensions).Select(i => (float)random.NextDouble()).ToArray()
                    );
                }).ToList();
                return new GeneratedEmbeddings<Embedding<float>>(embeddings);
            });

        mock.Setup(g => g.GetService(typeof(EmbeddingGeneratorMetadata), null))
            .Returns(new EmbeddingGeneratorMetadata(modelName));

        return mock;
    }

    private static Mock<IEmbeddingGenerator<string, Embedding<float>>> CreateNormalizedMockGenerator(string modelName, int dimensions = 384)
    {
        var mock = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        
        mock.Setup(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> values, EmbeddingGenerationOptions? _, CancellationToken _) =>
            {
                var list = values.ToList();
                var embeddings = list.Select(text =>
                {
                    var random = new Random(text.GetHashCode());
                    var vector = Enumerable.Range(0, dimensions).Select(i => (float)random.NextDouble()).ToArray();
                    
                    var magnitude = MathF.Sqrt(vector.Sum(v => v * v));
                    for (int i = 0; i < vector.Length; i++)
                    {
                        vector[i] /= magnitude;
                    }
                    
                    return new Embedding<float>(vector);
                }).ToList();
                return new GeneratedEmbeddings<Embedding<float>>(embeddings);
            });

        mock.Setup(g => g.GetService(typeof(EmbeddingGeneratorMetadata), null))
            .Returns(new EmbeddingGeneratorMetadata(modelName));

        return mock;
    }
}
