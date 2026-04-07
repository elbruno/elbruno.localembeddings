using ElBruno.LocalEmbeddings.VectorData.InMemory;
using ElBruno.LocalEmbeddings.VectorData.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;

namespace ElBruno.LocalEmbeddings.VectorData.Tests;

public class InMemoryVectorStoreTests
{
    [Fact]
    public void AddLocalEmbeddingsWithInMemoryVectorStore_ResolvesNamedCollectionViaDi()
    {
        var services = new ServiceCollection();

        services
            .AddLocalEmbeddingsWithInMemoryVectorStore(options =>
            {
                options.EnsureModelDownloaded = false;
                options.ModelPath = "unused-for-registration";
            })
            .AddVectorStoreCollection<int, ProductRecord>("products");

        using var provider = services.BuildServiceProvider();

        var vectorStore = provider.GetRequiredService<VectorStore>();
        var collection = provider.GetRequiredService<VectorStoreCollection<int, ProductRecord>>();

        Assert.IsType<InMemoryVectorStore>(vectorStore);
        Assert.Equal("products", collection.Name);
    }

    [Fact]
    public async Task UpsertAndGet_RecordLifecycle_Works()
    {
        var store = new InMemoryVectorStore();
        var collection = store.GetCollection<int, ProductRecord>("products");

        var record = new ProductRecord
        {
            Id = 7,
            Name = "Keyboard",
            Category = "Accessories",
            Tags = ["computer", "office"],
            Vector = new float[] { 1f, 0f }
        };

        await collection.UpsertAsync(record);
        var loaded = await collection.GetAsync(7);

        Assert.NotNull(loaded);
        Assert.Equal("Keyboard", loaded!.Name);
    }

    [Fact]
    public async Task SearchAsync_ReturnsNearestRecordsInOrder()
    {
        var store = new InMemoryVectorStore();
        var collection = store.GetCollection<int, ProductRecord>("products");

        await collection.UpsertAsync(new[]
        {
            new ProductRecord { Id = 1, Name = "Web Browser", Category = "Software", Tags = ["web"], Vector = new float[] { 1f, 0f } },
            new ProductRecord { Id = 2, Name = "Dotnet SDK", Category = "Software", Tags = ["dotnet"], Vector = new float[] { 0.9f, 0.1f } },
            new ProductRecord { Id = 3, Name = "iOS Toolkit", Category = "Mobile", Tags = ["ios"], Vector = new float[] { 0f, 1f } }
        });

        var results = await ToListAsync(collection.SearchAsync(new Embedding<float>(new[] { 1f, 0f }), top: 2));

        Assert.Equal(2, results.Count);
        Assert.Equal(1, results[0].Record.Id);
        Assert.Equal(2, results[1].Record.Id);
        Assert.True(results[0].Score >= results[1].Score);
    }

    [Fact]
    public async Task SearchAsync_EmptyCollection_ReturnsEmpty()
    {
        var store = new InMemoryVectorStore();
        var collection = store.GetCollection<int, ProductRecord>("products");

        var results = await ToListAsync(collection.SearchAsync(new[] { 1f, 0f }, top: 3));

        Assert.Empty(results);
    }

    [Fact]
    public async Task ConcurrentUpsertAndSearch_DoesNotThrow()
    {
        var store = new InMemoryVectorStore();
        var collection = store.GetCollection<int, ProductRecord>("products");

        var upsertTask = Task.Run(async () =>
        {
            for (var i = 0; i < 100; i++)
            {
                await collection.UpsertAsync(new ProductRecord
                {
                    Id = i,
                    Name = $"Item {i}",
                    Category = "Load",
                    Tags = ["bulk"],
                    Vector = new float[] { 1f, 0f }
                });
            }
        });

        var searchTask = Task.Run(async () =>
        {
            for (var i = 0; i < 25; i++)
            {
                _ = await ToListAsync(collection.SearchAsync(new[] { 1f, 0f }, top: 5));
            }
        });

        await Task.WhenAll(upsertTask, searchTask);

        var all = await ToListAsync(collection.GetAsync(static r => true, top: 200));
        Assert.NotEmpty(all);
    }

    [Fact]
    public void MissingVectorAnnotation_ThrowsClearException()
    {
        var store = new InMemoryVectorStore();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            store.GetCollection<int, InvalidRecord>("invalid"));

        Assert.Contains("VectorStoreVector", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListCollectionNamesAsync_ReturnsCreatedCollections()
    {
        var store = new InMemoryVectorStore();

        // Create some collections
        _ = store.GetCollection<int, ProductRecord>("alpha");
        _ = store.GetCollection<int, ProductRecord>("beta");
        _ = store.GetCollection<int, ProductRecord>("gamma");

        var names = await ToListAsync(store.ListCollectionNamesAsync());

        Assert.Equal(3, names.Count);
        Assert.Contains("alpha", names);
        Assert.Contains("beta", names);
        Assert.Contains("gamma", names);
    }

    [Fact]
    public async Task CollectionExistsAsync_ReturnsTrue_AfterEnsure()
    {
        var store = new InMemoryVectorStore();

        // Before creation
        bool existsBefore = await store.CollectionExistsAsync("products");
        Assert.False(existsBefore);

        // Create the collection via GetCollection
        _ = store.GetCollection<int, ProductRecord>("products");

        // After creation
        bool existsAfter = await store.CollectionExistsAsync("products");
        Assert.True(existsAfter);
    }

    [Fact]
    public async Task EnsureCollectionDeletedAsync_RemovesCollection()
    {
        var store = new InMemoryVectorStore();

        // Create and verify existence
        _ = store.GetCollection<int, ProductRecord>("products");
        Assert.True(await store.CollectionExistsAsync("products"));

        // Delete
        await store.EnsureCollectionDeletedAsync("products");

        // Verify removal
        Assert.False(await store.CollectionExistsAsync("products"));

        // Listing should not include the deleted collection
        var names = await ToListAsync(store.ListCollectionNamesAsync());
        Assert.DoesNotContain("products", names);
    }

    private static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> source)
    {
        var result = new List<T>();
        await foreach (var item in source)
        {
            result.Add(item);
        }

        return result;
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
        public ReadOnlyMemory<float> Vector { get; init; }
    }

    private sealed class InvalidRecord
    {
        [VectorStoreKey]
        public int Id { get; init; }

        [VectorStoreData]
        public required string Name { get; init; }
    }
}
