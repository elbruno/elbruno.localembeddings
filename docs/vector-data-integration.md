# VectorData Integration — ElBruno.LocalEmbeddings

Use the companion package `ElBruno.LocalEmbeddings.VectorData` to combine local embedding generation with `Microsoft.Extensions.VectorData` abstractions.

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

services.AddLocalEmbeddingsWithVectorStore(
    _ => CreateYourVectorStore(), // e.g. your provider implementation
    options =>
    {
        options.ModelName = "sentence-transformers/all-MiniLM-L6-v2";
        options.MaxSequenceLength = 256;
    });
```

After registration, DI exposes:

- `IEmbeddingGenerator<string, Embedding<float>>` (from `ElBruno.LocalEmbeddings`)
- `VectorStore` (from your provider factory)

## Register a typed collection

```csharp
services.AddVectorStoreCollection<int, ProductRecord>("products");
```

Then resolve and use it:

```csharp
var collection = provider.GetRequiredService<VectorStoreCollection<int, ProductRecord>>();
```

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
