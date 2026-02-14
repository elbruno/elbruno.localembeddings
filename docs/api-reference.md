# API Reference — ElBruno.LocalEmbeddings

## Packages

| Package | Description |
|---------|-------------|
| `ElBruno.LocalEmbeddings` | Core library — local ONNX embedding generation with M.E.AI |
| `ElBruno.LocalEmbeddings.KernelMemory` | Companion package — Kernel Memory `ITextEmbeddingGenerator` adapter + builder/DI extensions |
| `ElBruno.LocalEmbeddings.VectorData` | Companion package — DI helpers for `Microsoft.Extensions.VectorData` (`VectorStore` + typed collections) |

## LocalEmbeddingGenerator

The main class for generating embeddings. Implements `IEmbeddingGenerator<string, Embedding<float>>`.

```csharp
public sealed class LocalEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    // Constructors
    public LocalEmbeddingGenerator();
    public LocalEmbeddingGenerator(LocalEmbeddingsOptions options);

    // Async factory methods
    public static Task<LocalEmbeddingGenerator> CreateAsync(
        CancellationToken cancellationToken = default);

    public static Task<LocalEmbeddingGenerator> CreateAsync(
        LocalEmbeddingsOptions options,
        CancellationToken cancellationToken = default);

    // Properties
    public EmbeddingGeneratorMetadata Metadata { get; }

    // Methods
    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default);

    public TService? GetService<TService>(object? key = null) where TService : class;

    public void Dispose();
}
```

### Usage examples

```csharp
// Default model + default options
using var generator = new LocalEmbeddingGenerator();

// Custom options
using var customGenerator = new LocalEmbeddingGenerator(new LocalEmbeddingsOptions
{
    ModelName = "sentence-transformers/all-MiniLM-L12-v2"
});

// Async factory with defaults
using var asyncGenerator = await LocalEmbeddingGenerator.CreateAsync();
```

## LocalEmbeddingsOptions

Configuration options for the embedding generator.

```csharp
public sealed class LocalEmbeddingsOptions
{
    public string ModelName { get; set; }
    public string? ModelPath { get; set; }
    public string? CacheDirectory { get; set; }
    public int MaxSequenceLength { get; set; }
    public bool EnsureModelDownloaded { get; set; }
    public bool NormalizeEmbeddings { get; set; }
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ModelName` | `string` | `"sentence-transformers/all-MiniLM-L6-v2"` | HuggingFace model identifier |
| `ModelPath` | `string?` | `null` | Path to a local model directory (bypasses download) |
| `CacheDirectory` | `string?` | `null` | Custom directory for model cache |
| `MaxSequenceLength` | `int` | `512` | Maximum token sequence length |
| `EnsureModelDownloaded` | `bool` | `true` | Download model on startup if not cached |
| `NormalizeEmbeddings` | `bool` | `false` | Normalize vectors to unit length |

## EmbeddingGeneratorExtensions

Convenience extension methods on `IEmbeddingGenerator<string, Embedding<float>>` for single-string embedding generation. These live in the `ElBruno.LocalEmbeddings` namespace, so they appear automatically — no extra `using` required.

```csharp
public static class EmbeddingGeneratorExtensions
{
    // Returns a GeneratedEmbeddings collection (single item)
    public static Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        this IEmbeddingGenerator<string, Embedding<float>> generator,
        string value,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default);

    // Returns a single Embedding<float> directly
    public static Task<Embedding<float>> GenerateEmbeddingAsync(
        this IEmbeddingGenerator<string, Embedding<float>> generator,
        string value,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

### Usage examples

```csharp
// GenerateAsync — single string, returns collection
var result = await generator.GenerateAsync("Hello, world!");
float[] vector = result[0].Vector.ToArray();

// GenerateEmbeddingAsync — single string, returns embedding directly
var embedding = await generator.GenerateEmbeddingAsync("Hello, world!");
float[] vector = embedding.Vector.ToArray();

// Batch processing (existing API) — still available
var results = await generator.GenerateAsync(new[] { "text1", "text2", "text3" });
```

> **Tip:** Use `GenerateEmbeddingAsync` when you have a single text and want the embedding directly. Use the batch `GenerateAsync(IEnumerable<string>)` when processing multiple texts for better throughput.

## EmbeddingExtensions

Utility methods for embedding comparison and retrieval.

```csharp
public static class EmbeddingExtensions
{
    public static float CosineSimilarity(this ReadOnlyMemory<float> a, ReadOnlyMemory<float> b);

    public static float CosineSimilarity(this Embedding<float> a, Embedding<float> b);

    // All-pairs similarity matrix (single collection)
    public static float[,] Similarity(this IEnumerable<Embedding<float>> embeddings);

    // All-pairs similarity matrix (cross-collection)
    public static float[,] Similarity(
        this IEnumerable<Embedding<float>> embeddings1,
        IEnumerable<Embedding<float>> embeddings2);

    public static List<(T Item, float Score)> FindClosest<T>(
        this IEnumerable<(T Item, Embedding<float> Embedding)> items,
        Embedding<float> query,
        int topK = 5,
        float minScore = 0.0f);
}
```

### Similarity matrix example

```csharp
using ElBruno.LocalEmbeddings.Extensions;

var embeddings = await generator.GenerateAsync(new[]
{
    "The weather is lovely today.",
    "It's so sunny outside!",
    "He drove to the stadium."
});

var matrix = embeddings.Similarity();
Console.WriteLine(matrix[0, 1]); // sentence 0 vs sentence 1
```

## ServiceCollectionExtensions

Extension methods for DI registration in `ElBruno.LocalEmbeddings.Extensions` namespace.

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLocalEmbeddings(
        this IServiceCollection services,
        Action<LocalEmbeddingsOptions>? configure = null);

    public static IServiceCollection AddLocalEmbeddings(
        this IServiceCollection services,
        LocalEmbeddingsOptions options);

    public static IServiceCollection AddLocalEmbeddings(
        this IServiceCollection services,
        string modelName);

    public static IServiceCollection AddLocalEmbeddings(
        this IServiceCollection services,
        IConfiguration configuration);
}
```

---

## ElBruno.LocalEmbeddings.KernelMemory

Companion package providing Kernel Memory integration. Install separately:

```bash
dotnet add package ElBruno.LocalEmbeddings.KernelMemory
```

### LocalEmbeddingTextGenerator

Adapter that bridges `IEmbeddingGenerator<string, Embedding<float>>` (M.E.AI) to Kernel Memory's `ITextEmbeddingGenerator`. Namespace: `ElBruno.LocalEmbeddings.KernelMemory`.

```csharp
public sealed class LocalEmbeddingTextGenerator : ITextEmbeddingGenerator, IDisposable
{
    // Constructor
    public LocalEmbeddingTextGenerator(
        IEmbeddingGenerator<string, Embedding<float>> generator,
        int maxTokens = 512,
        ITextTokenizer? customTokenizer = null,
        bool ownsGenerator = false);

    // Properties
    public int MaxTokens { get; }

    // Methods (ITextEmbeddingGenerator)
    public Task<Embedding> GenerateEmbeddingAsync(
        string text, CancellationToken cancellationToken = default);

    // Methods (ITextTokenizer)
    public int CountTokens(string text);
    public IReadOnlyList<string> GetTokens(string text);

    public void Dispose();
}
```

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `generator` | `IEmbeddingGenerator<string, Embedding<float>>` | — | The M.E.AI embedding generator to wrap |
| `maxTokens` | `int` | `512` | Maximum tokens the model supports |
| `customTokenizer` | `ITextTokenizer?` | `null` | Optional custom tokenizer for accurate token counting |
| `ownsGenerator` | `bool` | `false` | Whether the adapter disposes the generator |

### KernelMemoryBuilderExtensions

Extension methods for `IKernelMemoryBuilder`. Namespace: `ElBruno.LocalEmbeddings.KernelMemory.Extensions`.

```csharp
public static class KernelMemoryBuilderExtensions
{
    // Default options (sentence-transformers/all-MiniLM-L6-v2)
    public static IKernelMemoryBuilder WithLocalEmbeddings(
        this IKernelMemoryBuilder builder);

    // Pre-built options
    public static IKernelMemoryBuilder WithLocalEmbeddings(
        this IKernelMemoryBuilder builder,
        LocalEmbeddingsOptions options);

    // Delegate configuration
    public static IKernelMemoryBuilder WithLocalEmbeddings(
        this IKernelMemoryBuilder builder,
        Action<LocalEmbeddingsOptions> configure);

    // Wrap an existing IEmbeddingGenerator
    public static IKernelMemoryBuilder WithLocalEmbeddings(
        this IKernelMemoryBuilder builder,
        IEmbeddingGenerator<string, Embedding<float>> generator,
        int maxTokens = 512);
}
```

### ServiceCollectionExtensions (Kernel Memory)

Extension methods for `IServiceCollection` that register both M.E.AI and Kernel Memory interfaces. Namespace: `ElBruno.LocalEmbeddings.KernelMemory.Extensions`.

```csharp
public static class ServiceCollectionExtensions
{
    // Delegate configuration
    public static IServiceCollection AddLocalEmbeddingsWithKernelMemory(
        this IServiceCollection services,
        Action<LocalEmbeddingsOptions>? configure = null);

    // Pre-built options
    public static IServiceCollection AddLocalEmbeddingsWithKernelMemory(
        this IServiceCollection services,
        LocalEmbeddingsOptions options);

    // IConfiguration binding
    public static IServiceCollection AddLocalEmbeddingsWithKernelMemory(
        this IServiceCollection services,
        IConfiguration configuration);
}
```

After calling `AddLocalEmbeddingsWithKernelMemory`, both interfaces resolve from the container:

- `IEmbeddingGenerator<string, Embedding<float>>` — for M.E.AI consumers
- `ITextEmbeddingGenerator` — for Kernel Memory consumers

---

## ElBruno.LocalEmbeddings.VectorData

Companion package providing `Microsoft.Extensions.VectorData` integration. Install separately:

```bash
dotnet add package ElBruno.LocalEmbeddings.VectorData
```

### ServiceCollectionExtensions (VectorData)

Extension methods for `IServiceCollection` in namespace `ElBruno.LocalEmbeddings.VectorData.Extensions`.

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLocalEmbeddingsWithVectorStore(
        this IServiceCollection services,
        Func<IServiceProvider, VectorStore> vectorStoreFactory,
        Action<LocalEmbeddingsOptions>? configure = null);

    public static IServiceCollection AddLocalEmbeddingsWithVectorStore(
        this IServiceCollection services,
        Func<IServiceProvider, VectorStore> vectorStoreFactory,
        LocalEmbeddingsOptions options);

    public static IServiceCollection AddLocalEmbeddingsWithVectorStore(
        this IServiceCollection services,
        Func<IServiceProvider, VectorStore> vectorStoreFactory,
        IConfiguration configuration);

    public static IServiceCollection AddVectorStoreCollection<TKey, TRecord>(
        this IServiceCollection services,
        string collectionName,
        VectorStoreCollectionDefinition? definition = null)
        where TKey : notnull
        where TRecord : class;
}
```
