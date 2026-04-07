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
using Microsoft.Extensions.AI;

public sealed class MyService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddings;

    public MyService(IEmbeddingGenerator<string, Embedding<float>> embeddings)
    {
        _embeddings = embeddings;
    }

    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        var result = await _embeddings.GenerateAsync([text]);
        return result[0].Vector.ToArray();
    }
}
```

---

## Multi-Model Scenarios: DI Registration Conflicts

### Registration conflict when using both base and Harrier

Both `AddLocalEmbeddings()` and `AddHarrierEmbeddings()` register the same interface: `IEmbeddingGenerator<string, Embedding<float>>`.

```csharp
// ⚠️ Only ONE of these registrations will "win"
services.AddLocalEmbeddings();                // Registers base (MiniLM)
services.AddHarrierEmbeddings();              // Tries to register Harrier — SKIPPED

// Result: IEmbeddingGenerator resolves to LocalEmbeddingGenerator (MiniLM)
// Harrier is NOT registered.
```

Both extensions use `TryAddSingleton`, which means **the first registration wins** — subsequent registrations are silently ignored.

### Solutions for multi-model scenarios

#### Option 1: Use keyed services (recommended for .NET 8+)

```csharp
services.AddKeyedSingleton<IEmbeddingGenerator<string, Embedding<float>>>(
    "miniLm",
    (sp, _) => new LocalEmbeddingGenerator(new LocalEmbeddingsOptions()));

services.AddKeyedSingleton<IEmbeddingGenerator<string, Embedding<float>>>(
    "harrier",
    async (sp, _) => await HarrierEmbeddingGenerator.CreateAsync());

// Resolve explicitly:
var miniLm = sp.GetRequiredKeyedService<IEmbeddingGenerator<string, Embedding<float>>>("miniLm");
var harrier = sp.GetRequiredKeyedService<IEmbeddingGenerator<string, Embedding<float>>>("harrier");
```

#### Option 2: Register one via DI, create the other explicitly

```csharp
// Register the primary model via DI
services.AddLocalEmbeddings();

// Create the secondary model manually when needed
var harrier = await HarrierEmbeddingGenerator.CreateAsync();
```

#### Option 3: Use wrapper service

```csharp
// Register a custom service that holds both
public sealed class EmbeddingService
{
    public EmbeddingService(
        IEmbeddingGenerator<string, Embedding<float>> miniLm,
        HarrierEmbeddingGenerator harrier)
    {
        MiniLm = miniLm;
        Harrier = harrier;
    }

    public IEmbeddingGenerator<string, Embedding<float>> MiniLm { get; }
    public HarrierEmbeddingGenerator Harrier { get; }
}

services.AddLocalEmbeddings();
services.AddSingleton(async sp => 
    await HarrierEmbeddingGenerator.CreateAsync());
services.AddSingleton<EmbeddingService>();
```

Inject `EmbeddingService` to access both generators.

---

### Harrier Integration

The companion package `ElBruno.LocalEmbeddings.Harrier` adds support for Microsoft Harrier-OSS-v1, the #1-ranked embedding model on MTEB-v2.

```bash
dotnet add package ElBruno.LocalEmbeddings.Harrier
```

`AddHarrierEmbeddings()` provides the same DI overloads as the base library:

```csharp
using ElBruno.LocalEmbeddings.Harrier.Extensions;

// 1) Basic
services.AddHarrierEmbeddings();

// 2) Configure with delegate
services.AddHarrierEmbeddings(options =>
{
    options.ModelVariant = HarrierModelVariant.Q4;
});

// 3) Pre-built options
services.AddHarrierEmbeddings(new HarrierEmbeddingsOptions { /* ... */ });

// 4) IConfiguration binding
services.AddHarrierEmbeddings(configuration.GetSection("HarrierEmbeddings"));
```

Harrier generates **640-dimensional** embeddings (vs. 384-dim for MiniLM) and is instruction-tuned for better quality. See [Harrier Integration](harrier-integration.md) for the full guide.

**⚠️ Important:** If you switch from MiniLM to Harrier, **vector stores must be re-indexed** due to dimension mismatch. See [Migration from MiniLM to Harrier](harrier-integration.md#migrating-from-minilm-to-harrier).

---

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

### Register LocalEmbeddings + built-in InMemoryVectorStore

```csharp
using ElBruno.LocalEmbeddings.VectorData.Extensions;

services.AddLocalEmbeddingsWithInMemoryVectorStore(options =>
{
    options.ModelName = "sentence-transformers/all-MiniLM-L6-v2";
});
```

For external providers, use `AddLocalEmbeddingsWithVectorStore(...)`.

### Register a typed collection

```csharp
services.AddVectorStoreCollection<int, ProductRecord>("products");
```

After registration, you can resolve:

- `IEmbeddingGenerator<string, Embedding<float>>`
- `VectorStore`
- `VectorStoreCollection<int, ProductRecord>`

See [VectorData Integration](vector-data-integration.md) for full usage details.

For a complete end-to-end sample using the shared in-memory store, see [samples/RagChat](../samples/RagChat).

---

### Deep Dives & Tutorials 🎓

Explore these integration patterns in detail:

- 📖 **[ElBruno.com blog](https://elbruno.com)** — Architecture patterns for DI, production deployment tips
- 🎬 **[YouTube channel](https://www.youtube.com/elbruno)** — Live demos of DI setup and troubleshooting
- 🎙️ **[No Tienen Nombre podcast](https://notienenombre.com)** — Discussions on framework choices and design patterns (Spanish)
