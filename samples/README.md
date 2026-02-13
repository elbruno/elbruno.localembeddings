# Samples — ElBruno.LocalEmbeddings

Four sample projects demonstrating LocalEmbeddings from basic usage to full RAG with a local LLM.

## Overview

| Sample | What It Shows | LLM Required? |
|--------|--------------|---------------|
| [ConsoleApp](#consoleapp) | Embedding basics: generation, similarity, search, DI | No |
| [RagChat](#ragchat) | Semantic search Q&A over an in-memory FAQ dataset | No |
| [RagOllama](#ragollama) | Full RAG chat using Ollama with phi4-mini + Kernel Memory | Yes (Ollama) |
| [RagFoundryLocal](#ragfoundrylocal) | Full RAG chat using Foundry Local with phi4-mini | Yes (Foundry Local) |

---

## ConsoleApp

**The best place to start.** Walks through 6 progressive examples in a single file:

1. Load the embedding model
2. Generate an embedding for a single string
3. Batch-embed multiple documents
4. Compare sentences with cosine similarity
5. Semantic search over a mini knowledge base
6. Dependency injection with `AddLocalEmbeddings()`

### Prerequisites

- .NET 10 SDK

### Run

```bash
dotnet run --project samples/ConsoleApp
```

---

## RagChat

A semantic search Q&A demo **without an LLM**. Embeds 20 FAQ documents about a fictional product, then lets you ask questions and see the most relevant answers ranked by similarity.

Shows: `InMemoryVectorStore`, `IEmbeddingGenerator` via DI, batch embedding with progress, interactive search loop.

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

---

## RagOllama

Full **Retrieval-Augmented Generation** combining LocalEmbeddings for retrieval and **Ollama** for LLM responses. Uses the companion package `ElBruno.LocalEmbeddings.KernelMemory` to integrate local ONNX embeddings with [Microsoft Kernel Memory](https://github.com/microsoft/kernel-memory).

- **Embeddings:** `LocalEmbeddingGenerator` via `.WithLocalEmbeddings()` (all-MiniLM-L6-v2, runs locally via ONNX)
- **Chat LLM:** phi4-mini via `OllamaSharp` (`IChatClient`)
- **Semantic Memory:** Microsoft Kernel Memory with `ElBruno.LocalEmbeddings.KernelMemory` adapter
- **Flow:** Import documents into Kernel Memory → ask a question → KM retrieves relevant chunks → generates an answer via the Ollama LLM

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

Same minimal flow as RagOllama, but using **Microsoft Foundry Local** instead of Ollama.

- **Embeddings:** `LocalEmbeddingGenerator` (all-MiniLM-L6-v2, runs locally via ONNX)
- **Chat LLM:** phi4-mini via `FoundryLocalManager` → OpenAI-compatible endpoint → `IChatClient`
- **Flow:** Ask once without memory, then ask again with embedding-based retrieved context

### Prerequisites

1. .NET 10 SDK
2. [Foundry Local](https://learn.microsoft.com/en-us/azure/ai-foundry/foundry-local/overview) installed
3. The phi4-mini model available locally (Foundry Local handles download)

### Run

```bash
dotnet run --project samples/RagFoundryLocal
```

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
