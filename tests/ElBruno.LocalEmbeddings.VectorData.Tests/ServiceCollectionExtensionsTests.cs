using ElBruno.LocalEmbeddings.VectorData.Extensions;
using ElBruno.LocalEmbeddings.VectorData.InMemory;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;
using Moq;

namespace ElBruno.LocalEmbeddings.VectorData.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddLocalEmbeddingsWithVectorStore_RegistersLocalEmbeddingsAndVectorStore()
    {
        var services = new ServiceCollection();

        services.AddLocalEmbeddingsWithVectorStore(_ => throw new InvalidOperationException("Factory should not be called while registering."));

        Assert.Contains(services, s => s.ServiceType == typeof(IEmbeddingGenerator<string, Embedding<float>>));
        Assert.Contains(services, s => s.ServiceType == typeof(VectorStore));
    }

    [Fact]
    public void AddVectorStoreCollection_RegistersTypedCollection()
    {
        var services = new ServiceCollection();

        services
            .AddLocalEmbeddingsWithVectorStore(_ => throw new InvalidOperationException("Factory should not be called while registering."))
            .AddVectorStoreCollection<int, SampleVectorRecord>("sample");

        Assert.Contains(services, s => s.ServiceType == typeof(VectorStoreCollection<int, SampleVectorRecord>));
    }

    [Fact]
    public void AddLocalEmbeddingsWithVectorStore_WithNullFactory_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
            services.AddLocalEmbeddingsWithVectorStore(null!));
    }

    [Fact]
    public void AddLocalEmbeddingsWithInMemoryVectorStore_RegistersVectorStore()
    {
        var services = new ServiceCollection();

        services.AddLocalEmbeddingsWithInMemoryVectorStore();

        using var provider = services.BuildServiceProvider();
        var vectorStore = provider.GetRequiredService<VectorStore>();

        Assert.IsType<InMemoryVectorStore>(vectorStore);
    }

    [Fact]
    public void AddVectorStoreCollection_WithInvalidCollectionName_ThrowsArgumentException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            services.AddVectorStoreCollection<int, SampleVectorRecord>(" "));
    }

    [Fact]
    public void AddVectorStoreCollectionWithEmbeddings_RegistersCollectionWithGenerator()
    {
        var services = new ServiceCollection();

        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();

        services.AddSingleton(mockGenerator.Object);
        services.AddSingleton<VectorStore, InMemoryVectorStore>();
        services.AddVectorStoreCollectionWithEmbeddings<int, SampleVectorRecord>("sample", useEmbeddingGenerator: true);

        using var provider = services.BuildServiceProvider();
        var collection = provider.GetRequiredService<VectorStoreCollection<int, SampleVectorRecord>>();

        Assert.NotNull(collection);
        Assert.Equal("sample", collection.Name);
    }

    [Fact]
    public void AddVectorStoreCollectionWithEmbeddings_WithoutGenerator_StillRegistersCollection()
    {
        var services = new ServiceCollection();

        services.AddSingleton<VectorStore, InMemoryVectorStore>();
        services.AddVectorStoreCollectionWithEmbeddings<int, SampleVectorRecord>("sample", useEmbeddingGenerator: true);

        using var provider = services.BuildServiceProvider();
        var collection = provider.GetRequiredService<VectorStoreCollection<int, SampleVectorRecord>>();

        Assert.NotNull(collection);
        Assert.Equal("sample", collection.Name);
    }

    [Fact]
    public void AddVectorStoreCollectionWithEmbeddings_WithUseEmbeddingGeneratorFalse_DoesNotConfigureGenerator()
    {
        var services = new ServiceCollection();

        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();

        services.AddSingleton(mockGenerator.Object);
        services.AddSingleton<VectorStore, InMemoryVectorStore>();
        services.AddVectorStoreCollectionWithEmbeddings<int, SampleVectorRecord>("sample", useEmbeddingGenerator: false);

        using var provider = services.BuildServiceProvider();
        var collection = provider.GetRequiredService<VectorStoreCollection<int, SampleVectorRecord>>();

        Assert.NotNull(collection);
    }

    [Fact]
    public void AddVectorStoreCollectionWithEmbeddings_WithInvalidCollectionName_ThrowsArgumentException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            services.AddVectorStoreCollectionWithEmbeddings<int, SampleVectorRecord>(""));
    }

    private sealed class SampleVectorRecord;
}
