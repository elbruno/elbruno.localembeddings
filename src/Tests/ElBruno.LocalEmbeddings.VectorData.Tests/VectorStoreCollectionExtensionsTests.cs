using ElBruno.LocalEmbeddings.VectorData.Extensions;
using ElBruno.LocalEmbeddings.VectorData.InMemory;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Moq;

namespace ElBruno.LocalEmbeddings.VectorData.Tests;

public class VectorStoreCollectionExtensionsTests
{
    [Fact]
    public async Task SearchByTextAsync_GeneratesEmbeddingAndSearches()
    {
        var store = new InMemoryVectorStore();
        var collection = store.GetCollection<int, ProductRecord>("products");

        await collection.UpsertAsync(new[]
        {
            new ProductRecord { Id = 1, Name = "Laptop", Category = "Electronics", Tags = ["computer"], Vector = new float[] { 0.9f, 0.1f } },
            new ProductRecord { Id = 2, Name = "Mouse", Category = "Accessories", Tags = ["computer"], Vector = new float[] { 0.8f, 0.2f } },
            new ProductRecord { Id = 3, Name = "Desk", Category = "Furniture", Tags = ["office"], Vector = new float[] { 0.1f, 0.9f } }
        });

        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        mockGenerator
            .Setup(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> values, EmbeddingGenerationOptions? _, CancellationToken _) =>
                new GeneratedEmbeddings<Embedding<float>>(values.Select(_ => new Embedding<float>(new float[] { 0.85f, 0.15f })).ToList()));

        var results = await collection.SearchByTextAsync(mockGenerator.Object, "laptop computer", top: 2);

        Assert.Equal(2, results.Count);
        Assert.Equal(1, results[0].Record.Id);
        Assert.Equal(2, results[1].Record.Id);
    }

    [Fact]
    public async Task SearchByTextAsync_WithNullQuery_ThrowsArgumentException()
    {
        var store = new InMemoryVectorStore();
        var collection = store.GetCollection<int, ProductRecord>("products");
        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            collection.SearchByTextAsync(mockGenerator.Object, null!, top: 5));
    }

    [Fact]
    public async Task SearchByTextAsync_WithEmptyQuery_ThrowsArgumentException()
    {
        var store = new InMemoryVectorStore();
        var collection = store.GetCollection<int, ProductRecord>("products");
        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            collection.SearchByTextAsync(mockGenerator.Object, "  ", top: 5));
    }

    [Fact]
    public async Task SearchByTextAsync_WithInvalidTop_ThrowsArgumentOutOfRangeException()
    {
        var store = new InMemoryVectorStore();
        var collection = store.GetCollection<int, ProductRecord>("products");
        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            collection.SearchByTextAsync(mockGenerator.Object, "query", top: 0));
    }

    [Fact]
    public async Task SearchByTextBatchAsync_GeneratesEmbeddingsAndSearches()
    {
        var store = new InMemoryVectorStore();
        var collection = store.GetCollection<int, ProductRecord>("products");

        await collection.UpsertAsync(new[]
        {
            new ProductRecord { Id = 1, Name = "Laptop", Category = "Electronics", Tags = ["computer"], Vector = new float[] { 0.9f, 0.1f } },
            new ProductRecord { Id = 2, Name = "Mouse", Category = "Accessories", Tags = ["computer"], Vector = new float[] { 0.8f, 0.2f } }
        });

        var queries = new[] { "laptop", "mouse" };

        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        mockGenerator
            .Setup(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>(
            [
                new Embedding<float>(new float[] { 0.9f, 0.1f }),
                new Embedding<float>(new float[] { 0.8f, 0.2f })
            ]));

        var results = await collection.SearchByTextBatchAsync(mockGenerator.Object, queries, top: 1);

        Assert.Equal(2, results.Count);
        Assert.Single(results[0]);
        Assert.Single(results[1]);
        Assert.Equal(1, results[0][0].Record.Id);
        Assert.Equal(2, results[1][0].Record.Id);
    }

    [Fact]
    public async Task SearchByTextBatchAsync_WithEmptyQueries_ReturnsEmpty()
    {
        var store = new InMemoryVectorStore();
        var collection = store.GetCollection<int, ProductRecord>("products");
        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();

        var results = await collection.SearchByTextBatchAsync(mockGenerator.Object, Array.Empty<string>(), top: 5);

        Assert.Empty(results);
        mockGenerator.Verify(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), null, default), Times.Never);
    }

    [Fact]
    public async Task UpsertWithEmbeddingAsync_GeneratesEmbeddingAndUpserts()
    {
        var store = new InMemoryVectorStore();
        var collection = store.GetCollection<int, ProductRecord>("products");

        var product = new ProductRecord
        {
            Id = 1,
            Name = "Laptop",
            Category = "Electronics",
            Tags = ["computer"],
            Vector = ReadOnlyMemory<float>.Empty
        };

        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        mockGenerator
            .Setup(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> values, EmbeddingGenerationOptions? _, CancellationToken _) =>
                new GeneratedEmbeddings<Embedding<float>>(values.Select(_ => new Embedding<float>(new float[] { 0.9f, 0.1f })).ToList()));

        await collection.UpsertWithEmbeddingAsync(
            mockGenerator.Object,
            product,
            p => $"{p.Name} {p.Category}",
            (p, embedding) => p.Vector = embedding.Vector);

        var loaded = await collection.GetAsync(1);

        Assert.NotNull(loaded);
        Assert.Equal(0.9f, loaded!.Vector.Span[0]);
        Assert.Equal(0.1f, loaded.Vector.Span[1]);
    }

    [Fact]
    public async Task UpsertWithEmbeddingAsync_WithNullRecord_ThrowsArgumentNullException()
    {
        var store = new InMemoryVectorStore();
        var collection = store.GetCollection<int, ProductRecord>("products");
        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            collection.UpsertWithEmbeddingAsync<int, ProductRecord>(
                mockGenerator.Object,
                null!,
                p => p.Name,
                (p, e) => p.Vector = e.Vector));
    }

    [Fact]
    public async Task UpsertBatchWithEmbeddingAsync_GeneratesEmbeddingsAndUpserts()
    {
        var store = new InMemoryVectorStore();
        var collection = store.GetCollection<int, ProductRecord>("products");

        var products = new[]
        {
            new ProductRecord { Id = 1, Name = "Laptop", Category = "Electronics", Tags = ["computer"], Vector = ReadOnlyMemory<float>.Empty },
            new ProductRecord { Id = 2, Name = "Mouse", Category = "Accessories", Tags = ["computer"], Vector = ReadOnlyMemory<float>.Empty }
        };

        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        mockGenerator
            .Setup(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>(
            [
                new Embedding<float>(new float[] { 0.9f, 0.1f }),
                new Embedding<float>(new float[] { 0.8f, 0.2f })
            ]));

        await collection.UpsertBatchWithEmbeddingAsync(
            mockGenerator.Object,
            products,
            p => p.Name,
            (p, embedding) => p.Vector = embedding.Vector);

        var laptop = await collection.GetAsync(1);
        var mouse = await collection.GetAsync(2);

        Assert.NotNull(laptop);
        Assert.NotNull(mouse);
        Assert.Equal(0.9f, laptop!.Vector.Span[0]);
        Assert.Equal(0.8f, mouse!.Vector.Span[0]);
        mockGenerator.Verify(g => g.GenerateAsync(It.Is<IEnumerable<string>>(q => q.Count() == 2), null, default), Times.Once);
    }

    [Fact]
    public async Task UpsertBatchWithEmbeddingAsync_WithEmptyRecords_DoesNothing()
    {
        var store = new InMemoryVectorStore();
        var collection = store.GetCollection<int, ProductRecord>("products");
        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();

        await collection.UpsertBatchWithEmbeddingAsync(
            mockGenerator.Object,
            Array.Empty<ProductRecord>(),
            p => p.Name,
            (p, e) => p.Vector = e.Vector);

        mockGenerator.Verify(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), null, default), Times.Never);
    }

    [Fact]
    public async Task SearchByTextAsync_WithFilter_AppliesFilter()
    {
        var store = new InMemoryVectorStore();
        var collection = store.GetCollection<int, ProductRecord>("products");

        await collection.UpsertAsync(new[]
        {
            new ProductRecord { Id = 1, Name = "Laptop", Category = "Electronics", Tags = ["computer"], Vector = new float[] { 0.9f, 0.1f } },
            new ProductRecord { Id = 2, Name = "Mouse", Category = "Accessories", Tags = ["computer"], Vector = new float[] { 0.8f, 0.2f } },
            new ProductRecord { Id = 3, Name = "Monitor", Category = "Electronics", Tags = ["display"], Vector = new float[] { 0.85f, 0.15f } }
        });

        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        mockGenerator
            .Setup(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<EmbeddingGenerationOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> values, EmbeddingGenerationOptions? _, CancellationToken _) =>
                new GeneratedEmbeddings<Embedding<float>>(values.Select(_ => new Embedding<float>(new float[] { 0.9f, 0.1f })).ToList()));

        var options = new VectorSearchOptions<ProductRecord>
        {
            Filter = r => r.Category == "Electronics"
        };

        var results = await collection.SearchByTextAsync(mockGenerator.Object, "computer", top: 5, options: options);

        Assert.All(results, r => Assert.Equal("Electronics", r.Record.Category));
    }

    private sealed class ProductRecord
    {
        [VectorStoreKey]
        public int Id { get; init; }

        [VectorStoreData]
        public required string Name { get; init; }

        [VectorStoreData]
        public required string Category { get; init; }

        [VectorStoreData]
        public required IReadOnlyList<string> Tags { get; init; }

        [VectorStoreVector(2, DistanceFunction = DistanceFunction.CosineSimilarity)]
        public ReadOnlyMemory<float> Vector { get; set; }
    }
}
