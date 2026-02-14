# Project Context

- **Owner:** Bruno Capuano (bcapuano@gmail.com)
- **Project:** LocalEmbeddings — a .NET library for local embedding generation using Microsoft.Extensions.AI abstractions
- **Stack:** .NET 10, C#, Microsoft.Extensions.AI, ONNX Runtime, HuggingFace models (all-MiniLM)
- **Created:** 2026-02-12

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-02-12: LocalEmbeddingGenerator Implementation
- Implemented `LocalEmbeddingGenerator` integrating with M.E.AI's `IEmbeddingGenerator<string, Embedding<float>>`
- The generator coordinates three internal components: `ModelDownloader`, `OnnxEmbeddingModel`, and `Tokenizer`
- Thread-safety is guaranteed after construction by the underlying ONNX Runtime session and tokenizer
- `EmbeddingGeneratorMetadata` uses `defaultModelId` and `defaultModelDimensions` (not `modelId`/`dimensions`)
- Options pattern supports both remote model download and local model path scenarios

### 2026-02-12: ServiceCollectionExtensions Polished
- Refactored DI extensions to use proper `IOptions<T>` pattern with `Microsoft.Extensions.Options`
- Added four overloads for `AddLocalEmbeddings`:
  1. `Action<LocalEmbeddingsOptions>?` - configure callback with Options pattern
  2. `LocalEmbeddingsOptions` - pre-configured instance directly
  3. `string modelName` - quick setup with just model name
  4. `IConfiguration` - bind from configuration section
- Registered `IModelDownloader` using `IHttpClientFactory` for proper HttpClient lifecycle
- Added comprehensive XML documentation with code examples for all public methods
- Added package references: `Microsoft.Extensions.Http`, `Microsoft.Extensions.Options`, `Microsoft.Extensions.Options.ConfigurationExtensions`

### 2026-02-12: RagChat Sample Application
- Created `samples/RagChat/` demonstrating RAG-style semantic search with local embeddings
- Key components:
  - `VectorStore/Document.cs` - Document model with Id, Title, Content, Embedding, and Category
  - `VectorStore/InMemoryVectorStore.cs` - Simple vector database with cosine similarity search
  - `Data/SampleData.cs` - 20 FAQ documents about fictional "LocalAI Assistant" product
  - `Program.cs` - Interactive console Q&A with progress indicators and colored output
- Pattern established: `InMemoryVectorStore` takes `IEmbeddingGenerator<string, Embedding<float>>` via constructor for DI
- Demonstrated batch embedding generation with progress callbacks
- Used `serviceProvider.GetRequiredService<>()` to resolve both the embedding generator and vector store from DI
