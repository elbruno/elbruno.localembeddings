# LocalEmbeddings

[![NuGet](https://img.shields.io/nuget/v/ElBruno.LocalEmbeddings.svg)](https://www.nuget.org/packages/ElBruno.LocalEmbeddings)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A .NET library for generating text embeddings locally using ONNX Runtime and Microsoft.Extensions.AI abstractions — no external API calls required.

## Features

- **Local Embedding Generation** — Run inference entirely on your machine using ONNX Runtime
- **Microsoft.Extensions.AI Integration** — Implements `IEmbeddingGenerator<string, Embedding<float>>`
- **Kernel Memory Integration** — Companion package `ElBruno.LocalEmbeddings.KernelMemory` provides a native `ITextEmbeddingGenerator` adapter for [Microsoft Kernel Memory](https://github.com/microsoft/kernel-memory)
- **HuggingFace Model Support** — Use popular sentence transformer models from HuggingFace Hub
- **Automatic Model Caching** — Models are downloaded once and cached locally
- **Dependency Injection Support** — First-class `IServiceCollection` integration
- **Thread-Safe & Batched** — Concurrent generation and efficient multi-text processing

## Installation

```bash
dotnet add package ElBruno.LocalEmbeddings
```

For **Kernel Memory** integration, also install the companion package:

```bash
dotnet add package ElBruno.LocalEmbeddings.KernelMemory
```

## Quick Start

```csharp
using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Options;

// Create the generator (downloads model automatically on first run)
using var generator = new LocalEmbeddingGenerator(new LocalEmbeddingsOptions());

// Generate an embedding
var result = await generator.GenerateAsync(["Hello, world!"]);

Console.WriteLine($"Dimensions: {result[0].Vector.Length}"); // 384
```

Want to go further? See the [Getting Started guide](docs/getting-started.md) for a step-by-step walkthrough — from cosine similarity to semantic search, dependency injection, and full RAG with a local LLM.

## Samples

See the [samples README](samples/README.md) for prerequisites and run instructions.

| Sample | What It Shows |
|--------|--------------|
| [ConsoleApp](samples/ConsoleApp) | All the basics: single/batch embeddings, similarity, semantic search, DI |
| [RagChat](samples/RagChat) | Embedding-only semantic search Q&A (no LLM needed) |
| [RagOllama](samples/RagOllama) | Full RAG with Ollama + phi-3.5-mini + Kernel Memory |
| [RagFoundryLocal](samples/RagFoundryLocal) | Full RAG with Foundry Local + phi-3.5-mini |

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
| [Dependency Injection](docs/dependency-injection.md) | All DI overloads and `IConfiguration` binding |
| [Kernel Memory Integration](docs/kernel-memory-integration.md) | Using local embeddings with Microsoft Kernel Memory |
| [Contributing](docs/contributing.md) | Build from source, repo structure, guidelines |
| [Publishing](docs/publishing.md) | NuGet publishing with GitHub Actions + Trusted Publishing |

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
