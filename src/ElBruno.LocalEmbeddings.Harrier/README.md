# ElBruno.LocalEmbeddings.Harrier

High-quality multilingual embeddings using **Microsoft Harrier-OSS-v1** locally with ONNX Runtime.

## Installation

```bash
dotnet add package ElBruno.LocalEmbeddings.Harrier
```

## Quick Start

```csharp
using ElBruno.LocalEmbeddings.Harrier;

// Create with default settings (downloads INT8 quantized model on first run)
await using var generator = await HarrierEmbeddingGenerator.CreateAsync();

// Generate embeddings
var embeddings = await generator.GenerateAsync(["Hello world!", "Hola mundo!"]);
Console.WriteLine($"Dimensions: {embeddings[0].Vector.Length}"); // 640
```

## Model Details

- **Publisher:** Microsoft (MIT license)
- **Architecture:** Decoder-only (Gemma 3 based)
- **Parameters:** 270M
- **Embedding dimensions:** 640
- **Context window:** 32,768 tokens
- **Languages:** 94+
- **MTEB-v2 ranking:** #1
- **Default variant:** INT8 Quantized (~270 MB)

## Features

- 🦅 **Top-ranked embedding model** on MTEB-v2 — best semantic quality
- 🌍 **Multilingual support** — 94+ languages
- 📦 **Multiple quantization variants** — Choose between quality (FP32/FP16) and speed (INT8/Q4)
- 🎯 **Instruction-tuned** — Specify task-specific prefixes for better results
- 📚 **Long context** — Supports up to 32,768 tokens
- ⚡ **ONNX Runtime** — Runs locally on CPU, zero external API calls
- 🔒 **Private by default** — No data sent to external services

## Configuration

```csharp
var options = new HarrierEmbeddingsOptions
{
    // Model variant (FP32, FP16, Quantized, Q4)
    ModelVariant = HarrierModelVariant.Quantized,
    
    // Instruction prefix for task-specific embeddings
    InstructionPrefix = "Instruct: Retrieve semantically similar text\nQuery: ",
    
    // Token limit
    MaxSequenceLength = 8192,
    
    // Cache and download
    EnsureModelDownloaded = true,
    CacheDirectory = null  // Auto-detect per platform
};

await using var generator = await HarrierEmbeddingGenerator.CreateAsync(options);
```

## Dependency Injection

```csharp
using ElBruno.LocalEmbeddings.Harrier.Extensions;
using Microsoft.Extensions.AI;

services.AddHarrierEmbeddings(options =>
{
    options.ModelVariant = HarrierModelVariant.Q4;
});

// Resolve as IEmbeddingGenerator<string, Embedding<float>>
var generator = serviceProvider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
```

## Learn More

📖 **[Full Harrier Integration Guide](../../docs/harrier-integration.md)** — Instruction prefixes, configuration, troubleshooting, and migration from MiniLM.

🎬 **[HarrierConsoleApp Sample](../../samples/HarrierConsoleApp)** — Complete working example with 6 progressive scenarios.

## License

MIT — See [LICENSE](../../LICENSE)
