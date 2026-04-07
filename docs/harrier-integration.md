# Harrier-OSS-v1 Integration Guide

## Overview

The `ElBruno.LocalEmbeddings.Harrier` package adds support for [Microsoft Harrier-OSS-v1](https://blogs.bing.com/search/April-2026/Microsoft-Open-Sources-Industry-Leading-Embedding-Model), the #1-ranked embedding model on MTEB-v2. It generates **640-dimensional** embeddings locally using ONNX Runtime — no external API calls required.

## Installation

```bash
dotnet add package ElBruno.LocalEmbeddings.Harrier
```

## Quick Start

```csharp
using ElBruno.LocalEmbeddings.Harrier;
using ElBruno.LocalEmbeddings.Harrier.Options;

// Create with default settings (downloads quantized INT8 model on first run)
await using var generator = await HarrierEmbeddingGenerator.CreateAsync();

// Generate embeddings
var embeddings = await generator.GenerateAsync(["Hello world!", "Hola mundo!"]);
Console.WriteLine($"Dimensions: {embeddings[0].Vector.Length}"); // 640
```

## Model Details

| Property | Value |
|----------|-------|
| Publisher | Microsoft (MIT license) |
| Architecture | Decoder-only (Gemma 3 based) |
| Parameters | 270M |
| Embedding dimensions | 640 |
| Context window | 32,768 tokens |
| Languages | 94+ |
| MTEB-v2 ranking | #1 |

### ONNX Variants

| Variant | Enum Value | Approximate Size | Notes |
|---------|-----------|-------------------|-------|
| FP32 | `HarrierModelVariant.Fp32` | ~1 GB | Highest accuracy |
| FP16 | `HarrierModelVariant.Fp16` | ~540 MB | Good balance |
| INT8 Quantized | `HarrierModelVariant.Quantized` | ~270 MB | **Default** — recommended |
| Q4 | `HarrierModelVariant.Q4` | ~196 MB | Smallest, fastest |

## Configuration

### Options

```csharp
var options = new HarrierEmbeddingsOptions
{
    // Model selection
    ModelName = "onnx-community/harrier-oss-v1-270m-ONNX",
    ModelVariant = HarrierModelVariant.Quantized, // Default
    
    // Instruction prefix (key for quality — see section below)
    InstructionPrefix = "Instruct: Retrieve semantically similar text\nQuery: ",
    
    // Tokenization
    MaxSequenceLength = 8192, // Default; model supports up to 32,768
    
    // Model download
    EnsureModelDownloaded = true,
    CacheDirectory = null, // Uses default cache location
    
    // ONNX Runtime performance
    UseParallelExecution = true,
    InterOpNumThreads = null, // Defaults to Environment.ProcessorCount
    IntraOpNumThreads = null, // Defaults to Environment.ProcessorCount
};

await using var generator = await HarrierEmbeddingGenerator.CreateAsync(options);
```

### Instruction Prefixes

Harrier is an **instruction-tuned** model. The instruction prefix tells the model what kind of embeddings to produce. Using the right prefix significantly improves quality for your task.

| Task | Instruction Prefix |
|------|-------------------|
| **Retrieval / Similarity** (default) | `"Instruct: Retrieve semantically similar text\nQuery: "` |
| **Web Search** | `"Instruct: Given a web search query, retrieve relevant passages that answer the query\nQuery: "` |
| **Bitext Mining** | `"Instruct: Retrieve parallel sentences\nQuery: "` |
| **Classification** | `"Instruct: Classify the following text\nQuery: "` |
| **Clustering** | `"Instruct: Identify the topic or theme of the following text\nQuery: "` |

> **Note:** Only queries need instruction prefixes. Documents/passages should be embedded without a prefix. To embed documents, create a second generator instance with `InstructionPrefix = null`.

### Using Pre-downloaded Models

If you've already downloaded the model, point directly to the directory:

```csharp
var options = new HarrierEmbeddingsOptions
{
    ModelPath = @"C:\models\harrier-oss-v1-270m",
    EnsureModelDownloaded = false
};
```

## Dependency Injection

```csharp
using ElBruno.LocalEmbeddings.Harrier.Extensions;

// Register with the Options pattern
services.AddHarrierEmbeddings(options =>
{
    options.ModelVariant = HarrierModelVariant.Q4;
    options.InstructionPrefix = "Instruct: Retrieve semantically similar text\nQuery: ";
});

// Or bind from IConfiguration
services.AddHarrierEmbeddings(configuration.GetSection("HarrierEmbeddings"));

// Resolve as IEmbeddingGenerator<string, Embedding<float>>
var generator = serviceProvider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
```

### appsettings.json Example

```json
{
  "HarrierEmbeddings": {
    "ModelVariant": "Quantized",
    "MaxSequenceLength": 8192,
    "InstructionPrefix": "Instruct: Retrieve semantically similar text\nQuery: "
  }
}
```

## Differences from Base Library

The Harrier package differs from the base `ElBruno.LocalEmbeddings` (all-MiniLM-L6-v2) in several ways:

| Aspect | Base Library | Harrier |
|--------|-------------|---------|
| Model type | BERT (encoder) | Gemma 3 (decoder-only) |
| Embedding size | 384 | 640 |
| Context window | 256 tokens | 32,768 tokens |
| Model file size | ~80 MB | ~270 MB (quantized) |
| Instruction prefix | Not needed | Required for best results |
| Multilingual | Limited | 94+ languages |
| Pooling | Mean pooling | Last-token (baked into ONNX) |
| Normalization | Manual L2 | Baked into ONNX |

## Token Counting

```csharp
int tokens = generator.CountTokens("Your text here");
Console.WriteLine($"Token count: {tokens}"); // Includes BOS, EOS, and instruction prefix tokens
```

## API Reference

### HarrierEmbeddingGenerator

```csharp
// Factory methods (async — downloads model if needed)
static Task<HarrierEmbeddingGenerator> CreateAsync(CancellationToken ct = default);
static Task<HarrierEmbeddingGenerator> CreateAsync(HarrierEmbeddingsOptions options, CancellationToken ct = default);
static Task<HarrierEmbeddingGenerator> CreateAsync(HarrierEmbeddingsOptions options, IProgress<double>? progress, CancellationToken ct = default);

// IEmbeddingGenerator<string, Embedding<float>> implementation
Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, ...);

// Token counting
int CountTokens(string text);

// Metadata
EmbeddingGeneratorMetadata Metadata { get; }
```

### HarrierEmbeddingsOptions

See [Configuration](#configuration) section above for all available options.

### HarrierModelVariant

```csharp
enum HarrierModelVariant { Fp32, Fp16, Quantized, Q4 }
```

## Troubleshooting

### Model download is slow or fails

The Harrier ONNX model is ~270 MB (quantized). On first run, it will be downloaded from HuggingFace. If the download fails:

1. Check your internet connection
2. Try a smaller variant: `ModelVariant = HarrierModelVariant.Q4` (~196 MB)
3. Pre-download the model manually and use `ModelPath`

### Out of memory

The 270M model requires approximately 1–2 GB of RAM during inference. If you encounter memory issues:

1. Use the `Q4` variant for lowest memory usage
2. Reduce `MaxSequenceLength` to limit input size
3. Process documents in smaller batches

### Tokenizer errors

If you see tokenizer-related errors, ensure the `tokenizer.json` file is present in the model directory alongside the ONNX model files.

## Migrating from MiniLM to Harrier

If you're currently using the base library's default MiniLM model and want to switch to Harrier, follow these steps:

### 1. Vector Store Re-indexing (⚠️ Required)

**Harrier produces 640-dimensional embeddings** while MiniLM produces **384-dimensional** embeddings. **Existing vector stores MUST be re-indexed.**

If you have documents embedded with MiniLM:

```csharp
// Old approach: MiniLM generates 384-dim embeddings
var oldGenerator = await LocalEmbeddingGenerator.CreateAsync();
var oldEmbeddings = await oldGenerator.GenerateAsync(documents);
// oldEmbeddings[0].Vector.Length == 384
```

These embeddings are **incompatible** with Harrier's 640-dim space. You must:

1. Re-generate embeddings using Harrier
2. Update your vector store with the new 640-dim vectors
3. Optionally, delete the old MiniLM cache to free space

```csharp
// New approach: Harrier generates 640-dim embeddings
var newGenerator = await HarrierEmbeddingGenerator.CreateAsync();
var newEmbeddings = await newGenerator.GenerateAsync(documents);
// newEmbeddings[0].Vector.Length == 640
```

### 2. Dependency Injection Swap

Replace `AddLocalEmbeddings()` with `AddHarrierEmbeddings()`. Both register the same interface, so the rest of your code stays the same:

**Before (MiniLM):**
```csharp
using ElBruno.LocalEmbeddings.Extensions;

services.AddLocalEmbeddings(options =>
{
    options.ModelName = "sentence-transformers/all-MiniLM-L6-v2";
});

// Resolved the same way
var generator = serviceProvider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
```

**After (Harrier):**
```csharp
using ElBruno.LocalEmbeddings.Harrier.Extensions;

services.AddHarrierEmbeddings(options =>
{
    options.ModelVariant = HarrierModelVariant.Quantized;
});

// Resolved the same way — interface is identical
var generator = serviceProvider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
```

### 3. Instruction Prefix Setup (Harrier-Specific)

Harrier is instruction-tuned. By default, `AddHarrierEmbeddings()` sets a retrieval instruction prefix, but you may want to customize it for your use case:

```csharp
services.AddHarrierEmbeddings(options =>
{
    // Default retrieval prefix
    options.InstructionPrefix = "Instruct: Retrieve semantically similar text\nQuery: ";
    
    // Or customize for your task
    options.InstructionPrefix = "Instruct: Given a web search query, retrieve relevant passages\nQuery: ";
});
```

**Important:** Only queries need the instruction prefix. Documents should **not** have a prefix. Create a second generator instance for document embedding:

```csharp
// For queries (with instruction prefix — default behavior)
var queryGenerator = serviceProvider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

// For documents (without prefix)
var docOptions = new HarrierEmbeddingsOptions
{
    InstructionPrefix = null  // Disable prefix for documents
};
var docGenerator = await HarrierEmbeddingGenerator.CreateAsync(docOptions);
```

### 4. Model Size Considerations

Harrier is larger than MiniLM:

| Aspect | MiniLM | Harrier |
|--------|--------|---------|
| Model file size | ~90 MB | ~500 MB (FP32) / ~270 MB (Quantized, default) |
| Memory during inference | ~500 MB | ~1.5–2 GB |
| Cache disk space | ~100 MB | ~300–500 MB |

If disk space is limited, use the smallest variant:

```csharp
var options = new HarrierEmbeddingsOptions
{
    ModelVariant = HarrierModelVariant.Q4  // ~196 MB
};
```

### 5. MaxSequenceLength (Optional Optimization)

Harrier supports very long sequences (up to 32,768 tokens) compared to MiniLM (512 tokens). If you don't need long context, lower `MaxSequenceLength` to reduce memory usage:

```csharp
var options = new HarrierEmbeddingsOptions
{
    MaxSequenceLength = 512  // Instead of default 8192 — saves memory
};
```

### Summary Checklist

- ✅ Back up or re-generate embeddings for existing vector stores (dimension change: 384 → 640)
- ✅ Replace `AddLocalEmbeddings()` with `AddHarrierEmbeddings()` in DI registration
- ✅ Configure instruction prefix for your task (or use the default retrieval prefix)
- ✅ Plan for ~3–4x larger model download and cache (100 MB → 300–500 MB)
- ✅ Optionally reduce `MaxSequenceLength` if memory is tight
- ✅ Test thoroughly — Harrier is higher quality but behaves differently (instruction tuning, decoder-based vs encoder)
