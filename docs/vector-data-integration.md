# VectorData Integration — ElBruno.LocalEmbeddings

Use the companion package `ElBruno.LocalEmbeddings.VectorData` to combine local embedding generation with `Microsoft.Extensions.VectorData` abstractions, including a built-in dependency-light `InMemoryVectorStore`.

## Install

```bash
dotnet add package ElBruno.LocalEmbeddings.VectorData
```

## Register with DI

```csharp
using ElBruno.LocalEmbeddings.VectorData.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;

var services = new ServiceCollection();

services.AddLocalEmbeddingsWithInMemoryVectorStore(
    options =>
    {
        options.ModelName = "sentence-transformers/all-MiniLM-L6-v2";
        options.MaxSequenceLength = 256;
    });
```

After registration, DI exposes:

- `IEmbeddingGenerator<string, Embedding<float>>` (from `ElBruno.LocalEmbeddings`)
- `VectorStore` (built-in `InMemoryVectorStore`)

## Register a typed collection

```csharp
services.AddVectorStoreCollection<int, ProductRecord>("products");
```

Then resolve and use it:

```csharp
var provider = services.BuildServiceProvider();
var collection = provider.GetRequiredService<VectorStoreCollection<int, ProductRecord>>();
```

You can still use `AddLocalEmbeddingsWithVectorStore(...)` when you want to plug in an external provider.

## Reference sample

See [samples/RagChat](../samples/RagChat) for a complete console sample that uses
`AddLocalEmbeddingsWithInMemoryVectorStore(...)` and `VectorStoreCollection<TKey, TRecord>` end-to-end.

## Record shape example

```csharp
using Microsoft.Extensions.VectorData;

public sealed class ProductRecord
{
    [VectorStoreKey]
    public int Id { get; init; }

    [VectorStoreData]
    public required string Name { get; init; }

    [VectorStoreData]
    public required string Description { get; init; }

    [VectorStoreVector(384, DistanceFunction = DistanceFunction.CosineSimilarity)]
    public ReadOnlyMemory<float> Vector { get; init; }
}
```

---

## Want a Full Example? 🚀

Check out [samples/RagChat](../samples/RagChat) for an end-to-end semantic search Q&A demo using `InMemoryVectorStore`. It's a great starting point for building your own vector-powered applications.

For deeper dives into vector database patterns:

- 📖 **[ElBruno.Com Blog](https://elbruno.com)** — Production patterns, vector indexing tips
- 🎥 **[YouTube Channel](https://www.youtube.com/elbruno)** — Live demos and architecture walkthroughs
- 🔗 **[Dependency Injection Guide](dependency-injection.md)** — All DI patterns for vector stores
