# Dependency Injection — ElBruno.LocalEmbeddings

`AddLocalEmbeddings()` provides four overloads for flexible registration of `IEmbeddingGenerator<string, Embedding<float>>`.

## 1) Basic registration

```csharp
using ElBruno.LocalEmbeddings.Extensions;

services.AddLocalEmbeddings();
```

## 2) Configure with delegate

```csharp
services.AddLocalEmbeddings(options =>
{
    options.ModelName = "sentence-transformers/all-MiniLM-L6-v2";
    options.MaxSequenceLength = 256;
    options.NormalizeEmbeddings = true;
});
```

## 3) Register with pre-built options

```csharp
var options = new LocalEmbeddingsOptions
{
    ModelName = "sentence-transformers/all-MiniLM-L6-v2",
    CacheDirectory = "/models/cache"
};

services.AddLocalEmbeddings(options);
```

## 4) Register with model name only

```csharp
services.AddLocalEmbeddings("sentence-transformers/all-MiniLM-L6-v2");
```

## 5) IConfiguration binding

```json
{
  "LocalEmbeddings": {
    "ModelName": "sentence-transformers/all-MiniLM-L6-v2",
    "MaxSequenceLength": 256,
    "NormalizeEmbeddings": true,
    "CacheDirectory": "/path/to/cache"
  }
}
```

```csharp
services.AddLocalEmbeddings(configuration.GetSection("LocalEmbeddings"));
```

## Injecting the generator

```csharp
public sealed class MyService(
    IEmbeddingGenerator<string, Embedding<float>> embeddings)
{
    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        var result = await embeddings.GenerateAsync([text]);
        return result[0].Vector.ToArray();
    }
}
```

---

## Kernel Memory Integration

The companion package `ElBruno.LocalEmbeddings.KernelMemory` adds DI extensions that register both the M.E.AI `IEmbeddingGenerator` and Kernel Memory's `ITextEmbeddingGenerator` from a single call.

```bash
dotnet add package ElBruno.LocalEmbeddings.KernelMemory
```

### 1) Basic registration

```csharp
using ElBruno.LocalEmbeddings.KernelMemory.Extensions;

services.AddLocalEmbeddingsWithKernelMemory();
```

### 2) Configure with delegate

```csharp
services.AddLocalEmbeddingsWithKernelMemory(options =>
{
    options.ModelName = "sentence-transformers/all-MiniLM-L6-v2";
    options.NormalizeEmbeddings = true;
});
```

### 3) Pre-built options

```csharp
var options = new LocalEmbeddingsOptions
{
    ModelName = "sentence-transformers/all-MiniLM-L6-v2",
    CacheDirectory = "/models/cache"
};
services.AddLocalEmbeddingsWithKernelMemory(options);
```

### 4) IConfiguration binding

```csharp
services.AddLocalEmbeddingsWithKernelMemory(
    configuration.GetSection("LocalEmbeddings"));
```

After calling any `AddLocalEmbeddingsWithKernelMemory` overload, both interfaces resolve from the container:

- `IEmbeddingGenerator<string, Embedding<float>>` — for M.E.AI consumers
- `ITextEmbeddingGenerator` — for Kernel Memory consumers

See [Kernel Memory Integration](kernel-memory-integration.md) for the full guide.

For retrieval-only pipelines built with `KernelMemoryBuilder`, use `WithLocalEmbeddingsSearchOnly()` to disable text generation requirements while keeping local embedding support.

---

## VectorData Integration

The companion package `ElBruno.LocalEmbeddings.VectorData` adds DI helpers for `Microsoft.Extensions.VectorData`.

```bash
dotnet add package ElBruno.LocalEmbeddings.VectorData
```

### Register LocalEmbeddings + VectorStore

```csharp
using ElBruno.LocalEmbeddings.VectorData.Extensions;

services.AddLocalEmbeddingsWithVectorStore(
    _ => CreateYourVectorStore(),
    options =>
    {
        options.ModelName = "sentence-transformers/all-MiniLM-L6-v2";
    });
```

### Register a typed collection

```csharp
services.AddVectorStoreCollection<int, ProductRecord>("products");
```

After registration, you can resolve:

- `IEmbeddingGenerator<string, Embedding<float>>`
- `VectorStore`
- `VectorStoreCollection<int, ProductRecord>`

See [VectorData Integration](vector-data-integration.md) for full usage details.
