# Zero-Cloud RAG Foundation Sample

Demonstrates the foundation of a Retrieval-Augmented Generation (RAG) pipeline using local embeddings - no cloud services required.

## What This Sample Does

This sample shows the core RAG retrieval pipeline:

1. **Initializes local embeddings** with `LocalEmbeddingGenerator.CreateAsync()`
2. **Creates a knowledge base** with documents about .NET topics
3. **Generates embeddings** locally using `sentence-transformers/all-MiniLM-L6-v2`
4. **Performs semantic search** with `FindClosestAsync` for multiple queries
5. **Demonstrates document similarity** comparison

**Note:** This sample focuses on the retrieval part of RAG. For a complete RAG example including local LLM integration, see the `LocalLlmRag` sample.

## Technologies Used

- **ElBruno.LocalEmbeddings** — Local embedding generation with ONNX Runtime

## Prerequisites

- .NET 10.0 SDK or later
- Approximately 80 MB of disk space for the embedding model (downloaded automatically on first run)
- Models will be cached in:
  - Windows: `%LOCALAPPDATA%\ElBruno\LocalEmbeddings\models\`
  - Linux/macOS: `~/.local/share/ElBruno/LocalEmbeddings/models/`

## How to Run

```bash
cd samples/ZeroCloudRag
dotnet run
```

On first run, the application will automatically download the embedding model. Subsequent runs will use the cached model.

## Sample Output

The application will:
1. Load the embedding model
2. Create a knowledge base with .NET documentation
3. Generate embeddings for all documents
4. Answer 4 sample queries using semantic search
5. Show document similarity comparisons

## Key Features

- **100% Offline** — No internet connection required after initial model download
- **No API Keys** — Everything runs locally
- **Fast** — Embeddings generated in milliseconds
- **Privacy-Focused** — Your data never leaves your computer
- **Simple Architecture** — Direct instantiation, minimal setup

## Next Steps

- **For complete RAG with LLM:** See the [LocalLlmRag](../LocalLlmRag/) sample
- **For tool routing:** See the [McpToolRouter](../McpToolRouter/) sample

## Learn More

- [LocalEmbeddings Documentation](../../docs/getting-started.md)
- [Semantic Search Patterns](../../docs/api-reference.md#semantic-search)
