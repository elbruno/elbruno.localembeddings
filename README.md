# LocalEmbeddings

[![NuGet](https://img.shields.io/nuget/v/ElBruno.LocalEmbeddings.svg)](https://www.nuget.org/packages/ElBruno.LocalEmbeddings)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A .NET library for generating text embeddings locally using ONNX Runtime and Microsoft.Extensions.AI abstractions — no external API calls required.

## Features

- **Local Embedding Generation** — Run inference entirely on your machine using ONNX Runtime
- **Microsoft.Extensions.AI Integration** — Implements `IEmbeddingGenerator<string, Embedding<float>>`
- **Kernel Memory Integration** — Companion package `ElBruno.LocalEmbeddings.KernelMemory` provides a native `ITextEmbeddingGenerator` adapter for [Microsoft Kernel Memory](https://github.com/microsoft/kernel-memory)
- **VectorData Integration** — Companion package `ElBruno.LocalEmbeddings.VectorData` adds DI helpers for `Microsoft.Extensions.VectorData` (`VectorStore` and typed collections)
- **HuggingFace Model Support** — Use popular sentence transformer models from HuggingFace Hub
- **Automatic Model Caching** — Models are downloaded once and cached locally
- **Dependency Injection Support** — First-class `IServiceCollection` integration
- **Single-String Convenience API** — `GenerateAsync("text")` and `GenerateEmbeddingAsync("text")` — no array wrapping needed
- **Similarity Helpers** — Cosine similarity, all-pairs `Similarity(...)` matrix, and `FindClosest(...)` for semantic search
- **Thread-Safe & Batched** — Concurrent generation and efficient multi-text processing

## Installation

```bash
dotnet add package ElBruno.LocalEmbeddings
```

For **Kernel Memory** integration, also install the companion package:

```bash
dotnet add package ElBruno.LocalEmbeddings.KernelMemory
```

For **VectorData** integration, install:

```bash
dotnet add package ElBruno.LocalEmbeddings.VectorData
```

## Quick Start

### 1) Generate one embedding

```csharp
using ElBruno.LocalEmbeddings;

await using var generator = await LocalEmbeddingGenerator.CreateAsync();
var embedding = await generator.GenerateEmbeddingAsync("Hello, world!");
Console.WriteLine(embedding.Vector.Length); // 384
```

### 2) Generate embeddings for multiple texts

```csharp
var inputs = new[] { "first text", "second text", "third text" };
var embeddings = await generator.GenerateAsync(inputs);
Console.WriteLine(embeddings.Count); // 3
```

### 3) Compare two texts with cosine similarity

```csharp
using ElBruno.LocalEmbeddings.Extensions;

var pair = await generator.GenerateAsync(["I love coding", "I enjoy programming"]);
var score = pair[0].CosineSimilarity(pair[1]);
Console.WriteLine(score);
```

For custom models and runtime behavior, use the options-based constructor:
`new LocalEmbeddingGenerator(new LocalEmbeddingsOptions { ... })`.

> **Note:** The synchronous constructor remains available for backward compatibility, but performs blocking initialization when downloads are needed.

Want to go further? Read the [Getting Started guide](docs/getting-started.md) and the other docs in this repo for DI, configuration, VectorData, Kernel Memory, and full RAG examples.

Prefer a containerized dev environment? See the Dev Container section in the [Contributing guide](docs/contributing.md#dev-container-vs-code).

## Samples

See the [samples README](samples/README.md) for prerequisites and run instructions.

| Sample | What It Shows |
|--------|--------------|
| [HelloWorldAltModel](samples/HelloWorldAltModel) | Minimal hello world with `sentence-transformers/all-MiniLM-L12-v2` |
| [ConsoleApp](samples/ConsoleApp) | All the basics: single/batch embeddings, similarity, semantic search, DI |
| [RagChat](samples/RagChat) | Embedding-only semantic search Q&A (no LLM needed) |
| [RagOllama](samples/RagOllama) | Full RAG with Ollama + phi4-mini + Kernel Memory |
| [RagFoundryLocal](samples/RagFoundryLocal) | Full RAG with Foundry Local + phi4-mini |

## Configuration

```csharp
var options = new LocalEmbeddingsOptions
{
    ModelName = "sentence-transformers/all-MiniLM-L6-v2",  // HuggingFace model
    MaxSequenceLength = 512,                                // Max tokens
    CacheDirectory = null,                                  // Auto-detect per platform
    EnsureModelDownloaded = true,                           // Download if missing
    NormalizeEmbeddings = false                              // L2 normalize vectors
};
```

See [Configuration docs](docs/configuration.md) for supported models, local model paths, and cache locations.

## Documentation

| Topic | Description |
|-------|-------------|
| [Getting Started](docs/getting-started.md) | Step-by-step guide from hello world to RAG |
| [API Reference](docs/api-reference.md) | Classes, methods, and extension methods |
| [Configuration](docs/configuration.md) | Options, supported models, cache locations |
| [Alternative Models](docs/alternative-models.md) | Non-default free models, local download workflow, and license notes |
| [Dependency Injection](docs/dependency-injection.md) | All DI overloads and `IConfiguration` binding |
| [Kernel Memory Integration](docs/kernel-memory-integration.md) | Using local embeddings with Microsoft Kernel Memory |
| [VectorData Integration](docs/vector-data-integration.md) | Using local embeddings with Microsoft.Extensions.VectorData abstractions |
| [Contributing](docs/contributing.md) | Build from source, repo structure, guidelines |
| [Roadmap](docs/plans/roadmap_260213_0803.md) | Planned and completed features/samples with priorities |
| [Publishing](docs/publishing.md) | NuGet publishing with GitHub Actions + Trusted Publishing |
| [Changelog](docs/changelog.md) | Versioned summary of notable changes |

Have an idea for a new feature or sample? Please open an issue and share your suggestion.

## Building from Source

```bash
git clone https://github.com/elbruno/elbruno.localembeddings.git
cd elbruno.localembeddings
dotnet build
dotnet test
```

## Requirements

- .NET 10.0 SDK or later
- ONNX Runtime compatible platform (Windows, Linux, macOS)

## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
