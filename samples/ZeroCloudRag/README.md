# Zero-Cloud RAG Sample

A complete offline Retrieval-Augmented Generation (RAG) pipeline that requires no cloud services or API keys.

## What This Sample Does

This sample demonstrates a fully local RAG system that:

1. **Sets up dependency injection** with `AddLocalEmbeddings()` and `AddLocalLLMs()`
2. **Creates a knowledge base** with hardcoded documents about .NET development topics
3. **Generates embeddings** locally using the `sentence-transformers/all-MiniLM-L6-v2` model
4. **Stores documents** in an in-memory vector store
5. **Accepts user queries** and retrieves the top-K most relevant documents using semantic search
6. **Generates answers** using a local LLM (Phi-4) with the retrieved context
7. **Provides interactive mode** for asking multiple questions

## Technologies Used

- **ElBruno.LocalEmbeddings** — Local embedding generation with ONNX Runtime
- **ElBruno.LocalEmbeddings.VectorData** — In-memory vector storage
- **ElBruno.LocalLLMs** — Local LLM inference (Phi-4)
- **ElBruno.LocalLLMs.Rag** — RAG utilities and patterns
- **Microsoft.Extensions.Hosting** — Dependency injection and service lifetime management

## Prerequisites

- .NET 10.0 SDK or later
- Approximately 2-3 GB of disk space for models (downloaded automatically on first run)
- Models will be cached in:
  - Windows: `%LOCALAPPDATA%\ElBruno\LocalEmbeddings\models\`
  - Linux/macOS: `~/.local/share/ElBruno/LocalEmbeddings/models/`

## How to Run

```bash
cd samples/ZeroCloudRag
dotnet run
```

On first run, the application will automatically download:
- The embedding model (~80 MB)
- The Phi-4 model (~2.5 GB)

Subsequent runs will use the cached models and start immediately.

## Sample Output

The application will:
1. Load the embedding model and LLM
2. Create a knowledge base with .NET documentation
3. Generate embeddings for all documents
4. Answer a sample query: "How do I build web applications with .NET?"
5. Enter interactive mode where you can ask your own questions

## Key Features

- **100% Offline** — No internet connection required after initial model download
- **No API Keys** — Everything runs locally on your machine
- **Fast** — Embeddings generated in milliseconds, LLM responses in seconds
- **Privacy-Focused** — Your data never leaves your computer
- **Production-Ready Patterns** — Uses dependency injection and clean architecture

## Learn More

- [LocalEmbeddings Documentation](../../docs/getting-started.md)
- [VectorData Integration](../../docs/vector-data-integration.md)
- [RAG Patterns and Best Practices](../../docs/rag-patterns.md)
