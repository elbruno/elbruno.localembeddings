using Microsoft.Extensions.AI;

namespace ElBruno.LocalEmbeddings.Tests;

public class EmbeddingGeneratorFindClosestTests
{
    [Fact]
    public async Task FindClosestAsync_WithMismatchedCorpusEmbeddings_ThrowsArgumentException()
    {
        var generator = new FakeEmbeddingGenerator();
        var corpus = new[] { "doc1", "doc2" };
        var corpusEmbeddings = new[] { new Embedding<float>(new[] { 1f, 0f }) };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            generator.FindClosestAsync("query", corpus, corpusEmbeddings));
    }

    [Fact]
    public async Task FindClosestAsync_WhenCorpusEmbeddingsNull_GeneratesCorpusEmbeddings()
    {
        var generator = new FakeEmbeddingGenerator(new Dictionary<string, float[]>
        {
            ["query"] = [1f, 0f],
            ["a"] = [1f, 0f],
            ["b"] = [0f, 1f],
            ["c"] = [0.5f, 0.5f]
        });

        var corpus = new[] { "a", "b", "c" };
        var results = await generator.FindClosestAsync("query", corpus, topK: 2);

        Assert.Equal(2, results.Count);
        Assert.Equal(2, generator.Requests.Count);
        Assert.Contains(generator.Requests, request => request.SequenceEqual(corpus));
    }

    [Fact]
    public async Task FindClosestAsync_HonorsMinScoreFilter()
    {
        var generator = new FakeEmbeddingGenerator(new Dictionary<string, float[]>
        {
            ["query"] = [1f, 0f],
            ["doc1"] = [1f, 0f],
            ["doc2"] = [0f, 1f]
        });

        var corpus = new[] { "doc1", "doc2" };
        var results = await generator.FindClosestAsync("query", corpus, topK: 10, minScore: 0.5f);

        Assert.Single(results);
        Assert.Equal("doc1", results[0].Text);
        Assert.Equal(0, results[0].Index);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    [InlineData(5, 3)]
    public async Task FindClosestAsync_TopKBoundaries_ReturnExpectedCount(int topK, int expectedCount)
    {
        var generator = new FakeEmbeddingGenerator(new Dictionary<string, float[]>
        {
            ["query"] = [1f, 0f],
            ["doc1"] = [1f, 0f],
            ["doc2"] = [0.9f, 0.1f],
            ["doc3"] = [0f, 1f]
        });

        var corpus = new[] { "doc1", "doc2", "doc3" };
        var results = await generator.FindClosestAsync("query", corpus, topK: topK);

        Assert.Equal(expectedCount, results.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task FindClosestAsync_WithInvalidTopK_ThrowsArgumentOutOfRangeException(int topK)
    {
        var generator = new FakeEmbeddingGenerator();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            generator.FindClosestAsync("query", new[] { "doc1" }, topK: topK));
    }

    [Fact]
    public async Task FindClosestAsync_TypedOverload_MapsTextWithSelector()
    {
        var generator = new FakeEmbeddingGenerator(new Dictionary<string, float[]>
        {
            ["web"] = [1f, 0f],
            ["C# for .NET"] = [0.7f, 0.3f],
            ["JavaScript for browsers"] = [1f, 0f],
            ["Swift for iOS"] = [0f, 1f]
        });

        var corpus = new[]
        {
            new SearchDoc("A", "C# for .NET"),
            new SearchDoc("B", "JavaScript for browsers"),
            new SearchDoc("C", "Swift for iOS")
        };

        var results = await generator.FindClosestAsync(
            query: "web",
            corpus: corpus,
            textSelector: static doc => doc.Text,
            topK: 2);

        Assert.Equal(2, results.Count);
        Assert.Equal(1, results[0].Index);
        Assert.Equal("JavaScript for browsers", results[0].Text);
    }

    private sealed record SearchDoc(string Id, string Text);

    private sealed class FakeEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        private readonly IReadOnlyDictionary<string, float[]> _vectors;

        public FakeEmbeddingGenerator(IReadOnlyDictionary<string, float[]>? vectors = null)
        {
            _vectors = vectors ?? new Dictionary<string, float[]>
            {
                ["query"] = [1f, 0f],
                ["doc1"] = [1f, 0f]
            };
        }

        public List<string[]> Requests { get; } = [];

        public EmbeddingGeneratorMetadata Metadata { get; } = new(
            providerName: "Fake",
            providerUri: new Uri("https://example.com"),
            defaultModelId: "fake-model",
            defaultModelDimensions: 2);

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var inputs = values.ToArray();
            Requests.Add(inputs);

            var embeddings = inputs
                .Select(value => _vectors.TryGetValue(value, out var vector)
                    ? new Embedding<float>(vector)
                    : new Embedding<float>(new[] { 0f, 0f }))
                .ToList();

            return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(embeddings));
        }

        public TService? GetService<TService>(object? key = null)
            where TService : class =>
            typeof(TService) == typeof(IEmbeddingGenerator<string, Embedding<float>>) ? (TService)(object)this : null;

        public object? GetService(Type serviceType, object? key = null)
        {
            ArgumentNullException.ThrowIfNull(serviceType);
            return serviceType == typeof(IEmbeddingGenerator<string, Embedding<float>>) ? this : null;
        }

        public void Dispose()
        {
        }
    }
}
