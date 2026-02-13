# Kernel Memory Integration — ElBruno.LocalEmbeddings

Use local ONNX-based embeddings with [Microsoft Kernel Memory](https://github.com/microsoft/kernel-memory) for semantic memory, RAG pipelines, and document ingestion — all running locally, no external API calls required.

## Installation

```bash
dotnet add package ElBruno.LocalEmbeddings.KernelMemory
```

This installs the companion package which transitively brings in:

- `ElBruno.LocalEmbeddings` (core library)
- `Microsoft.KernelMemory.Abstractions`
- `Microsoft.KernelMemory.Core`

## Quick Start — KernelMemoryBuilder

The simplest way to use local embeddings with Kernel Memory:

```csharp
using ElBruno.LocalEmbeddings.KernelMemory.Extensions;
using Microsoft.KernelMemory;

var memory = new KernelMemoryBuilder()
    .WithLocalEmbeddings()          // <-- one line!
    .WithOllamaTextGeneration(config)
    .Build();

// Import documents
await memory.ImportTextAsync("Bruno's favourite super hero is Invincible", "doc1");

// Ask questions
var answer = await memory.AskAsync("What is Bruno's favourite super hero?");
Console.WriteLine(answer.Result);
```

## Builder Overloads

### Default options

Uses `sentence-transformers/all-MiniLM-L6-v2` with 512 max tokens:

```csharp
builder.WithLocalEmbeddings();
```

### Custom options

```csharp
builder.WithLocalEmbeddings(new LocalEmbeddingsOptions
{
    ModelName = "sentence-transformers/all-MiniLM-L6-v2",
    NormalizeEmbeddings = true,
    MaxSequenceLength = 256
});
```

### Delegate configuration

```csharp
builder.WithLocalEmbeddings(options =>
{
    options.NormalizeEmbeddings = true;
    options.MaxSequenceLength = 256;
});
```

### Wrap an existing generator

If you already have an `IEmbeddingGenerator<string, Embedding<float>>` (e.g., from DI):

```csharp
builder.WithLocalEmbeddings(existingGenerator, maxTokens: 512);
```

The adapter does **not** take ownership — the caller manages the generator's lifecycle.

## Dependency Injection

For host-based applications, register both M.E.AI and Kernel Memory interfaces in one call:

```csharp
using ElBruno.LocalEmbeddings.KernelMemory.Extensions;

services.AddLocalEmbeddingsWithKernelMemory(options =>
{
    options.ModelName = "sentence-transformers/all-MiniLM-L6-v2";
});
```

Both interfaces then resolve from the container:

| Interface | Use Case |
|-----------|----------|
| `IEmbeddingGenerator<string, Embedding<float>>` | M.E.AI consumers, custom vector stores |
| `ITextEmbeddingGenerator` | Kernel Memory pipelines |

### DI Overloads

```csharp
// Delegate configuration
services.AddLocalEmbeddingsWithKernelMemory(options => { ... });

// Pre-built options
services.AddLocalEmbeddingsWithKernelMemory(new LocalEmbeddingsOptions { ... });

// IConfiguration binding (appsettings.json)
services.AddLocalEmbeddingsWithKernelMemory(
    configuration.GetSection("LocalEmbeddings"));
```

## How It Works

The companion package provides a thin adapter layer:

```
┌─────────────────────────────┐
│     Kernel Memory           │
│   (ITextEmbeddingGenerator) │
└──────────┬──────────────────┘
           │ delegates to
           ▼
┌─────────────────────────────┐
│  LocalEmbeddingTextGenerator│  ← adapter (this package)
│  (ITextEmbeddingGenerator)  │
└──────────┬──────────────────┘
           │ wraps
           ▼
┌─────────────────────────────┐
│  LocalEmbeddingGenerator    │  ← core package
│  (IEmbeddingGenerator)      │
└──────────┬──────────────────┘
           │ runs
           ▼
┌─────────────────────────────┐
│  ONNX Runtime               │
│  (all-MiniLM-L6-v2)        │
└─────────────────────────────┘
```

### The Adapter — `LocalEmbeddingTextGenerator`

- Implements Kernel Memory's `ITextEmbeddingGenerator` (which includes `ITextTokenizer`)
- Wraps any `IEmbeddingGenerator<string, Embedding<float>>` — not just `LocalEmbeddingGenerator`
- Converts M.E.AI `Embedding<float>` to KM `Embedding` (both wrap `ReadOnlyMemory<float>`)
- Token counting uses a word-boundary heuristic by default; supply a custom `ITextTokenizer` for precise counts
- Thread-safe when the underlying generator is thread-safe

### Why a Separate Package?

The core `ElBruno.LocalEmbeddings` has **zero** Kernel Memory dependencies — it only depends on `Microsoft.Extensions.AI.Abstractions`. This keeps it lightweight for consumers who use M.E.AI directly or integrate with other frameworks. The companion package adds the KM bridge without bloating the core.

## Migrating from the Manual Bridge

If you previously used `WithCustomEmbeddingGenerator<T>()` directly:

**Before** (manual bridge):

```csharp
using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Options;
using Microsoft.Extensions.AI;
using Microsoft.KernelMemory;

using var generator = new LocalEmbeddingGenerator(new LocalEmbeddingsOptions());

var memory = new KernelMemoryBuilder()
    .WithOllamaTextGeneration(config)
    .WithCustomEmbeddingGenerator<IEmbeddingGenerator<string, Embedding<float>>>(generator)
    .Build();
```

**After** (companion package):

```csharp
using ElBruno.LocalEmbeddings.KernelMemory.Extensions;
using Microsoft.KernelMemory;

var memory = new KernelMemoryBuilder()
    .WithOllamaTextGeneration(config)
    .WithLocalEmbeddings()
    .Build();
```

The companion package handles generator construction, adapter wrapping, and lifecycle management automatically.

## Complete Example — RAG with Ollama

See [samples/RagOllama](../samples/RagOllama) for a full working example:

```csharp
using ElBruno.LocalEmbeddings.KernelMemory.Extensions;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI.Ollama;

var config = new OllamaConfig
{
    Endpoint = "http://localhost:11434",
    TextModel = new OllamaModelConfig("phi4-mini")
};

var memory = new KernelMemoryBuilder()
    .WithOllamaTextGeneration(config)
    .WithLocalEmbeddings()
    .Build();

// Import facts
await memory.ImportTextAsync("Bruno's favourite super hero is Invincible", "1");
await memory.ImportTextAsync("Gisela's favourite super hero is Batman", "2");

// Ask a question — KM retrieves relevant docs + generates an answer via Ollama
var answer = await memory.AskAsync("What is Bruno's favourite super hero?");
Console.WriteLine(answer.Result);
```

## See Also

- [API Reference](api-reference.md) — Full type signatures for all adapter classes
- [Dependency Injection](dependency-injection.md) — All DI overloads including KM integration
- [Configuration](configuration.md) — Supported models, cache locations, options
- [Getting Started](getting-started.md) — Step-by-step guide from hello world to RAG
