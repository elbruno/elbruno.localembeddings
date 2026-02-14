using ElBruno.LocalEmbeddings.VectorData.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;

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
    public void AddVectorStoreCollection_WithInvalidCollectionName_ThrowsArgumentException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            services.AddVectorStoreCollection<int, SampleVectorRecord>(" "));
    }

    private sealed class SampleVectorRecord;
}
