# Local LLM + Embeddings Integration Sample

A simple demonstration of combining local embeddings and local language models for basic RAG (Retrieval-Augmented Generation).

## What This Sample Does

This sample shows how to integrate `ElBruno.LocalEmbeddings` with `ElBruno.LocalLLMs` to:

1. **Generate embeddings** for a collection of documents using `LocalEmbeddingGenerator`
2. **Perform semantic search** using the `FindClosest` extension method
3. **Summarize results** with a local language model (Phi-4)
4. **Compare embeddings** directly using cosine similarity

This is a simplified version of the full RAG pipeline shown in the `ZeroCloudRag` sample, demonstrating the core integration between the two libraries.

## Technologies Used

- **ElBruno.LocalEmbeddings** — Local embedding generation with ONNX Runtime
- **ElBruno.LocalLLMs** — Local LLM inference (Phi-4)

## Prerequisites

- .NET 10.0 SDK or later
- Approximately 2-3 GB of disk space for models (downloaded automatically on first run)
- Models will be cached in:
  - Windows: `%LOCALAPPDATA%\ElBruno\LocalEmbeddings\models\`
  - Linux/macOS: `~/.local/share/ElBruno/LocalEmbeddings/models/`

## How to Run

```bash
cd samples/LocalLlmRag
dotnet run
```

On first run, the application will automatically download:
- The embedding model (~80 MB)
- The Phi-4 model (~2.5 GB)

Subsequent runs will use the cached models and start immediately.

## Sample Output

The application will:
1. Initialize the embedding generator and local LLM
2. Create a knowledge base with facts about Seattle
3. Generate embeddings for all documents
4. Answer two semantic search queries with LLM-generated summaries
5. Demonstrate direct embedding similarity comparison

## Key Differences from ZeroCloudRag

| Feature | LocalLlmRag (This Sample) | ZeroCloudRag |
|---------|---------------------------|--------------|
| **DI Setup** | Direct instantiation | Full dependency injection with `IHost` |
| **Vector Store** | In-memory list | `InMemoryVectorStore` from VectorData |
| **Complexity** | Simple, minimal setup | Production-ready patterns |
| **Interactive Mode** | No | Yes |
| **Use Case** | Learning the basics | Production RAG template |

## When to Use This Pattern

- **Prototyping** — Quick experiments with embeddings + LLM
- **Learning** — Understanding how the libraries work together
- **Simple Apps** — Small console applications without DI infrastructure
- **Scripting** — One-off data processing scripts

For production applications, use the `ZeroCloudRag` sample's dependency injection pattern instead.

## Key Features

- **Minimal Setup** — No DI container, just direct instantiation
- **FindClosest Helper** — Built-in top-K search with scoring
- **Streaming LLM** — Real-time token-by-token output
- **100% Offline** — No cloud services required
- **Fast** — Embeddings in milliseconds, LLM responses in seconds

## Learn More

- [LocalEmbeddings Documentation](../../docs/getting-started.md)
- [LocalLLMs Documentation](https://github.com/elbruno/ElBruno.LocalLLMs)
- [ZeroCloudRag Sample](../ZeroCloudRag/) - Full RAG pipeline with DI
