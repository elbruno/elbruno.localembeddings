# LocalEmbeddings

[![NuGet](https://img.shields.io/nuget/v/ElBruno.LocalEmbeddings.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/ElBruno.LocalEmbeddings)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ElBruno.LocalEmbeddings.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/ElBruno.LocalEmbeddings)
[![Build Status](https://github.com/elbruno/elbruno.localembeddings/actions/workflows/publish.yml/badge.svg)](https://github.com/elbruno/elbruno.localembeddings/actions/workflows/publish.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](LICENSE)
[![GitHub stars](https://img.shields.io/github/stars/elbruno/elbruno.localembeddings?style=social)](https://github.com/elbruno/elbruno.localembeddings)
[![Twitter Follow](https://img.shields.io/twitter/follow/elbruno?style=social)](https://twitter.com/elbruno)

A .NET library for generating text embeddings locally using ONNX Runtime and Microsoft.Extensions.AI abstractions — no external API calls required.

## 🎥 Quick Overview

New to local embeddings? Watch this **[5-minute video](https://www.youtube.com/watch?v=0rRgSQWlVm8)** explaining the main goal of the library.

Want to build RAG applications? Read this **[blog post about 3 RAG approaches in .NET](https://elbruno.com/2026/02/14/%f0%9f%a4%96-building-rag-in-net-with-local-embeddings-3-approaches-zero-cloud-calls/)** with local embeddings and zero cloud calls.

Interested in **image embeddings**? Check out the **[YouTube video](https://www.youtube.com/watch?v=nVTropZJC88)** and **[blog post](https://elbruno.com/2026/02/16/%f0%9f%96%bc%ef%b8%8f-local-image-embeddings-in-net-clip-onnx/)** about local image embeddings with CLIP and ONNX.

## Features

- **Local Embedding Generation** — Run inference entirely on your machine using ONNX Runtime
- **Microsoft.Extensions.AI Integration** — Implements `IEmbeddingGenerator<string, Embedding<float>>`
- **Kernel Memory Integration** — Companion package `ElBruno.LocalEmbeddings.KernelMemory` provides a native `ITextEmbeddingGenerator` adapter for [Microsoft Kernel Memory](https://github.com/microsoft/kernel-memory)
- **VectorData Integration** — Companion package `ElBruno.LocalEmbeddings.VectorData` adds DI helpers for `Microsoft.Extensions.VectorData` (`VectorStore` and typed collections)
- **Built-in In-Memory Vector Store** — `ElBruno.LocalEmbeddings.VectorData` includes `InMemoryVectorStore` (no Semantic Kernel connector dependency required)
- **HuggingFace Model Support** — Use popular sentence transformer models from HuggingFace Hub
- **Automatic Model Caching** — Models are downloaded once and cached locally
- **Dependency Injection Support** — First-class `IServiceCollection` integration
- **Single-String Convenience API** — `GenerateAsync("text")` and `GenerateEmbeddingAsync("text")` — no array wrapping needed
- **Similarity Helpers** — Cosine similarity, all-pairs `Similarity(...)` matrix, and one-line `FindClosestAsync(...)` semantic search
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

### 4) Semantic search in one line

```csharp
var corpus = new[]
{
    "Python for data science",
    "JavaScript for web apps",
    "Swift for iOS development"
};

var corpusEmbeddings = await generator.GenerateAsync(corpus);
var results = await generator.FindClosestAsync(
    "best language for websites",
    corpus,
    corpusEmbeddings,
    topK: 2,
    minScore: 0.2f);

foreach (var result in results)
    Console.WriteLine($"{result.Score:F3} - {result.Text}");
```

For custom models and runtime behavior, use the options-based constructor:
`new LocalEmbeddingGenerator(new LocalEmbeddingsOptions { ... })`.

> **Note:** The synchronous constructor remains available for backward compatibility, but performs blocking initialization when downloads are needed.

Want to go further? Read the [Getting Started guide](docs/getting-started.md) and the other docs in this repo for DI, configuration, VectorData, Kernel Memory, and full RAG examples.

Prefer a containerized dev environment? See the Dev Container section in the [Contributing guide](docs/contributing.md#dev-container-vs-code).

## Samples

See the [samples README](samples/README.md) for prerequisites and run instructions.

| Sample | What It Shows |
| ------ | ------------- |
| [HelloWorldAltModel](samples/HelloWorldAltModel) | Minimal hello world with `sentence-transformers/all-MiniLM-L12-v2` |
| [ConsoleApp](samples/ConsoleApp) | All the basics: single/batch embeddings, similarity, semantic search, DI |
| [RagChat](samples/RagChat) | Embedding-only semantic search Q&A using shared VectorData `InMemoryVectorStore` (no LLM needed) |
| [RagOllama](samples/RagOllama) | Full RAG with Ollama + phi4-mini + Kernel Memory |
| [RagFoundryLocal](samples/RagFoundryLocal) | Full RAG with Foundry Local + phi4-mini |
| [ImageRagSimple](samples/ImageRagSimple) | Minimal image RAG: index images → search by text |
| [ImageRagChat](samples/ImageRagChat) | Interactive image RAG chat with text and image-to-image search |

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

### Common model options (with model cards)

Estimated download sizes below are approximate and can vary by ONNX variant (fp32/int8) and tokenizer assets.

- [`sentence-transformers/all-MiniLM-L6-v2` (default, ~90–100 MB)](https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2)
- [`sentence-transformers/all-MiniLM-L12-v2` (~130–140 MB)](https://huggingface.co/sentence-transformers/all-MiniLM-L12-v2)
- [`sentence-transformers/paraphrase-MiniLM-L6-v2` (~90–100 MB)](https://huggingface.co/sentence-transformers/paraphrase-MiniLM-L6-v2)
- [`BAAI/bge-large-en-v1.5` (large, ~1.3 GB)](https://huggingface.co/BAAI/bge-large-en-v1.5)
- [`intfloat/e5-large-v2` (large, ~1.3 GB)](https://huggingface.co/intfloat/e5-large-v2)

## Documentation

| Topic | Description |
| ----- | ----------- |
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

## 👋 About the Author

Hi! I'm **ElBruno** 🧡, a passionate developer and content creator exploring AI, .NET, and modern development practices.

**Made with ❤️ by [ElBruno](https://github.com/elbruno)**

If you like this project, consider following my work across platforms:

- 📻 **Podcast**: [No Tienen Nombre](https://notienenombre.com) — Spanish-language episodes on AI, development, and tech culture
- 💻 **Blog**: [ElBruno.com](https://elbruno.com) — Deep dives on embeddings, RAG, .NET, and local AI
- 📺 **YouTube**: [youtube.com/elbruno](https://www.youtube.com/elbruno) — Demos, tutorials, and live coding
- 🔗 **LinkedIn**: [@elbruno](https://www.linkedin.com/in/elbruno/) — Professional updates and insights
- 𝕏 **Twitter**: [@elbruno](https://www.x.com/in/elbruno/) — Quick tips, releases, and tech news

## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
