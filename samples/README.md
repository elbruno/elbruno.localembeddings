# Samples — ElBruno.LocalEmbeddings

Nine sample projects demonstrating LocalEmbeddings from basic usage to full RAG with a local LLM.

## Overview

| Sample | What It Shows | LLM Required? |
|--------|--------------|---------------|
| [HelloWorldAltModel](#helloworldaltmodel) | Minimal hello world with a non-default free model | No |
| [RaspberryPiTiny](#raspberrypitiny) | Ultra-small sample for Raspberry Pi and low-memory devices | No |
| [ConsoleAppLite](#consoleapplite) | Lightweight menu sample for low-resource devices | No |
| [ConsoleApp](#consoleapp) | Embedding basics: generation, similarity, search, DI | No |
| [BenchmarkSample](#benchmarksample) | BenchmarkDotNet performance suite with cross-platform baselines | No |
| [RagChat](#ragchat) | Semantic search Q&A over an in-memory FAQ dataset | No |
| [ImageSearchSample](#imagesearchsample) | Multimodal CLIP text-to-image search with ONNX Runtime | No |
| [RagOllama](#ragollama) | Full RAG chat using Ollama with phi4-mini + Kernel Memory | Yes (Ollama) |
| [RagFoundryLocal](#ragfoundrylocal) | Full RAG chat using Foundry Local with phi4-mini | Yes (Foundry Local) |

---

## Image Samples (Multimodal RAG)

For all image-related samples (searching images with text), please refer to the dedicated guide:

👉 **[Image Embeddings Samples & Setup](README_IMAGES.md)**

It covers:

- Downloading the required CLIP models (`.onnx`)
- Using the shared sample images
- Running the `ImageRagSimple`, `ImageRagChat`, and `ImageSearchSample` projects

---

## HelloWorldAltModel

Simple hello world sample using a free non-default model:

- **Embedding model:** `sentence-transformers/all-MiniLM-L12-v2` (Apache-2.0)
- **What it does:** generates one embedding and prints dimensions

### Prerequisites

- .NET 10 SDK
- Internet access on first run to download model files

### Run

```bash
dotnet run --project samples/HelloWorldAltModel
```

---

## RaspberryPiTiny

Smallest sample focused on device stability.

- Default mode runs **one** embedding and exits
- Optional mode computes similarity for **two** short texts
- Uses conservative runtime settings (`ORT_SEQUENTIAL`, one thread)

### Prerequisites

- .NET 10 SDK

### Run

Default (single embedding):

```bash
dotnet run --project samples/RaspberryPiTiny
```

Similarity mode (two embeddings + cosine similarity):

```bash
dotnet run --project samples/RaspberryPiTiny -- sim
```

---

## ConsoleApp

**The best place to start.** Walks through 6 progressive examples in a single file:

1. Load the embedding model
2. Generate an embedding for a single string (using `GenerateAsync("text")` convenience overload)
3. Batch-embed multiple documents
4. Compare sentences with cosine similarity and all-pairs `Similarity(...)` matrix
5. Semantic search over a mini knowledge base (using `GenerateEmbeddingAsync(query)`)
6. Dependency injection with `AddLocalEmbeddings()`

### Prerequisites

- .NET 10 SDK

### Run

```bash
dotnet run --project samples/ConsoleApp
```

---

## ConsoleAppLite

Lightweight sample designed for low-resource environments (for example Raspberry Pi).

- Loads one model once
- Runs only one small scenario
- Exits immediately (no long full-demo flow)

### Scenarios

1. Generate one embedding (Hello World)
2. Generate two embeddings and compute cosine similarity

### Run

Interactive menu:

```bash
dotnet run --project samples/ConsoleAppLite
```

Run a single scenario directly:

```bash
dotnet run --project samples/ConsoleAppLite -- 1
dotnet run --project samples/ConsoleAppLite -- 2
```

---

## BenchmarkSample

[BenchmarkDotNet](https://benchmarkdotnet.org/) performance suite for reproducible benchmarking of embedding generation, tokenization, and cosine similarity.

- **Embedding benchmarks:** single-text and batch (10/50/100) throughput
- **Similarity benchmarks:** TensorPrimitives cosine similarity (384/768-dim), nearest-neighbour search (100/1000 docs)
- **Tokenizer benchmarks:** short text, long text, and batch (10/50) tokenization
- **Cross-platform baselines:** strategy for capturing and comparing results on Windows and Linux

### Prerequisites

- .NET 10 SDK
- Model pre-downloaded (auto-downloads on first run)

### Run

All benchmarks:

```bash
dotnet run -c Release --project samples/BenchmarkSample
```

Specific class:

```bash
dotnet run -c Release --project samples/BenchmarkSample -- --filter "*EmbeddingBenchmarks*"
```

Export JSON for baseline comparison:

```bash
dotnet run -c Release --project samples/BenchmarkSample -- --exporters json
```

See [samples/BenchmarkSample/README.md](BenchmarkSample/README.md) for full details and baseline strategy.

---

## RagChat

A semantic search Q&A demo **without an LLM**. Embeds 20 FAQ documents about a fictional product, then lets you ask questions and see the most relevant answers ranked by similarity.

Shows: shared `ElBruno.LocalEmbeddings.VectorData.InMemory` store via `AddLocalEmbeddingsWithInMemoryVectorStore(...)`, `IEmbeddingGenerator` via DI, batch embedding with progress, interactive search loop, and Spectre.Console-powered rendering with menu/formatting helpers extracted into dedicated files.

### Prerequisites

- .NET 10 SDK

### Run

```bash
dotnet run --project samples/RagChat
```

### Commands

| Command | Action |
|---------|--------|
| `help`  | Show example questions |
| `list`  | Show all indexed documents |
| `quit`  | Exit |

### Try these models

You can quickly test different embedding models by changing `options.ModelName` in `samples/RagChat/Program.cs`.

Example:

```csharp
options.ModelName = "sentence-transformers/all-MiniLM-L12-v2";
```

Recommended models to try:

Estimated download sizes below are approximate and can vary by ONNX variant and tokenizer assets.

- [`sentence-transformers/all-MiniLM-L6-v2` (default, ~90–100 MB)](https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2)  
    Fast, lightweight baseline for most scenarios.
- [`sentence-transformers/all-MiniLM-L12-v2` (~130–140 MB)](https://huggingface.co/sentence-transformers/all-MiniLM-L12-v2)  
    Often better semantic quality with a small latency/memory increase.
- [`sentence-transformers/paraphrase-MiniLM-L6-v2` (~90–100 MB)](https://huggingface.co/sentence-transformers/paraphrase-MiniLM-L6-v2)  
    Good alternative for paraphrase-heavy or FAQ-style retrieval tests.
- [`BAAI/bge-large-en-v1.5` (large, ~1.3 GB)](https://huggingface.co/BAAI/bge-large-en-v1.5)  
    Higher-quality retrieval candidate when you can trade more memory and latency for quality.
- [`intfloat/e5-large-v2` (large, ~1.3 GB)](https://huggingface.co/intfloat/e5-large-v2)  
    Strong retrieval model option for larger-scale semantic matching tests.

Notes:

- The first run of a new model downloads and caches model artifacts.
- Keep `MaxSequenceLength` consistent when comparing models so results are easier to benchmark.

---

## ImageSearchSample

Text-to-image **semantic search** using CLIP (Contrastive Language–Image Pretraining) models running entirely locally via ONNX Runtime. Search a collection of images using natural language queries.

- **Embeddings:** CLIP text and vision encoders (separate ONNX models)
- **Search:** Natural language queries → find semantically matching images
- **Tokenization:** BPE tokenizer for CLIP (not WordPiece like BERT)
- **Image preprocessing:** Resize, normalize, NCHW format for CLIP vision encoder
- **Ranking:** Cosine similarity between query and image embeddings

### What It Demonstrates

- Loading and running **multimodal CLIP models** with ONNX Runtime
- Implementing **BPE tokenization** for text encoding
- **Image preprocessing** for CLIP vision encoding
- **Cross-modal search** (text → images)
- Indexing images by computing their embeddings
- Cosine similarity ranking for semantic search

### Prerequisites

1. .NET 10 SDK
2. **CLIP ONNX models** — text encoder, vision encoder, vocabulary, and merge files
3. **Sample images** — a directory of images to search

### CLIP Model Setup

Use the provided helper scripts to download the required models:

```powershell
# Windows
./scripts/download_clip_models.ps1
```

```bash
# Linux/macOS
./scripts/download_clip_models.sh
```

Alternatively, export from HuggingFace using Optimum CLI:

```bash
pip install optimum[exporters]
optimum-cli export onnx --model openai/clip-vit-base-patch32 ./clip-models/
```

Or download pre-exported models:

```bash
git clone https://huggingface.co/Xenova/clip-vit-base-patch32 clip-models
```

### Run

```bash
dotnet run --project samples/ImageSearchSample -- <model-directory> <image-directory>
```

Example:

```bash
dotnet run --project samples/ImageSearchSample -- ./clip-models ./my-images
```

### Example Queries

- "a cat sitting on a couch"
- "sunset over the ocean"
- "a person riding a bicycle"
- "a red car"

See [samples/ImageSearchSample/README.md](ImageSearchSample/README.md) for complete setup and usage instructions.

---

## RagOllama

Full **Retrieval-Augmented Generation** combining LocalEmbeddings for retrieval and **Ollama** for LLM responses. Uses the companion package `ElBruno.LocalEmbeddings.KernelMemory` to integrate local ONNX embeddings with [Microsoft Kernel Memory](https://github.com/microsoft/kernel-memory).

- **Embeddings:** `LocalEmbeddingGenerator` via `.WithLocalEmbeddings()` (all-MiniLM-L6-v2, runs locally via ONNX)
- **Chat LLM:** phi4-mini via `OllamaSharp` (`IChatClient`)
- **Semantic Memory:** Microsoft Kernel Memory with `ElBruno.LocalEmbeddings.KernelMemory` adapter
- **Flow:** Import facts into Kernel Memory → ask a question → KM retrieves relevant chunks → Ollama LLM generates an answer
- **Sample data:** Fun facts about people and movies — e.g. "What is Bruno's favourite super hero?"

### Approach

RagOllama uses **Microsoft Kernel Memory** as a high-level orchestrator. You call `memory.ImportTextAsync()` to ingest facts and `memory.AskStreamingAsync()` to query. Kernel Memory handles chunking, embedding, storage, retrieval, and prompt building internally. The `ElBruno.LocalEmbeddings.KernelMemory` companion package plugs local ONNX embeddings into this pipeline via the `ITextEmbeddingGenerator` adapter.

This is the **easiest path** if you want a turnkey RAG pipeline with minimal code.

### Prerequisites

1. .NET 10 SDK
2. [Ollama](https://ollama.com/) installed and running
3. Pull the [phi4-mini](https://ollama.com/library/phi4-mini) model:

```bash
ollama pull phi4-mini
ollama serve   # if not already running
```

### Run

```bash
dotnet run --project samples/RagOllama
```

---

## RagFoundryLocal

Full **Retrieval-Augmented Generation** using **Microsoft Foundry Local** for the LLM and **LocalEmbeddings** for retrieval. Uses the same sample data and question as RagOllama so you can compare the two approaches side by side.

- **Embeddings:** `LocalEmbeddingGenerator` (all-MiniLM-L6-v2, runs locally via ONNX)
- **Chat LLM:** phi4-mini via `FoundryLocalManager` → OpenAI-compatible endpoint → `IChatClient`
- **Flow:** Ask once without memory → embed facts + query with `LocalEmbeddingGenerator` → find closest matches with `FindClosest` → build a prompt with retrieved context → stream the LLM answer
- **Sample data:** Same facts as RagOllama — e.g. "What is Bruno's favourite super hero?"

### Approach

RagFoundryLocal implements the **RAG pipeline manually** — you control every step: embedding the facts, embedding the query, finding the closest matches with `FindClosest`, building the prompt with retrieved context, and streaming the response from the LLM. There is no Kernel Memory abstraction; you work directly with `LocalEmbeddingGenerator`, `EmbeddingExtensions`, and `IChatClient`.

This approach gives you **full control** over the retrieval and prompt-building steps, and demonstrates how the core `ElBruno.LocalEmbeddings` library works without any companion packages.

### Prerequisites

1. .NET 10 SDK
2. [Foundry Local](https://learn.microsoft.com/en-us/azure/ai-foundry/foundry-local/overview) installed
3. The phi4-mini model available locally (Foundry Local handles download)

### Run

```bash
dotnet run --project samples/RagFoundryLocal
```

---

## RagOllama vs RagFoundryLocal

Both samples answer the same question using the same facts, but take different approaches:

| Aspect | RagOllama | RagFoundryLocal |
|--------|-----------|-----------------|
| **LLM runtime** | Ollama | Microsoft Foundry Local |
| **Embedding integration** | `ElBruno.LocalEmbeddings.KernelMemory` adapter | `ElBruno.LocalEmbeddings` directly |
| **RAG orchestration** | Microsoft Kernel Memory (high-level) | Manual (embed → search → prompt → stream) |
| **Retrieval** | Automatic via `memory.AskStreamingAsync()` | Explicit via `FindClosest()` + `BuildPrompt()` |
| **Companion package** | Yes (`ElBruno.LocalEmbeddings.KernelMemory`) | No (core library only) |
| **Best for** | Turnkey RAG with minimal code | Full control over the pipeline |

---

## Architecture Comparison

```
ConsoleApp / RagChat                    RagOllama / RagFoundryLocal
┌─────────────────────┐                 ┌─────────────────────┐
│    User Query        │                 │    User Query        │
└────────┬────────────┘                 └────────┬────────────┘
         │                                       │
         ▼                                       ▼
┌─────────────────────┐                 ┌─────────────────────┐
│  LocalEmbeddings     │                 │  LocalEmbeddings     │
│  (embed + search)    │                 │  (embed + search)    │
└────────┬────────────┘                 └────────┬────────────┘
         │                                       │
         ▼                                       ▼
┌─────────────────────┐                 ┌─────────────────────┐
│  Display top results │                 │  Build prompt with   │
│  (similarity scores) │                 │  retrieved context   │
└─────────────────────┘                 └────────┬────────────┘
                                                  │
                                                  ▼
                                        ┌─────────────────────┐
                                        │  phi4-mini (LLM)     │
                                        │  via Ollama / Foundry │
                                        └────────┬────────────┘
                                                  │
                                                  ▼
                                        ┌─────────────────────┐
                                        │  Stream response to   │
                                        │  console              │
                                        └─────────────────────┘
```
