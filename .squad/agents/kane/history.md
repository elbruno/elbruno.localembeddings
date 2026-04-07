# Project Context

- **Owner:** Bruno Capuano
- **Project:** LocalEmbeddings — a .NET library for local embedding generation using Microsoft.Extensions.AI abstractions
- **Stack:** .NET 10, C#, Microsoft.Extensions.AI, ONNX Runtime, HuggingFace models (all-MiniLM)
- **Created:** 2026-02-12

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-04-04: VectorData Embedding Generation Integration

- Implemented `VectorStoreCollectionExtensions` providing text-to-vector search capabilities
- Four key extension methods on `VectorStoreCollection<TKey, TRecord>`:
  1. `SearchByTextAsync` — single text query with automatic embedding generation
  2. `SearchByTextBatchAsync` — batch text queries for efficiency
  3. `UpsertWithEmbeddingAsync` — upsert single record with automatic embedding from text
  4. `UpsertBatchWithEmbeddingAsync` — batch upsert with automatic embeddings
- Added `AddVectorStoreCollectionWithEmbeddings` DI method that wires `IEmbeddingGenerator` into `VectorStoreCollectionDefinition.EmbeddingGenerator` property
- All methods accept `IEmbeddingGenerator<string, Embedding<float>>` for flexibility — can use cached/decorated generators
- Pattern: `textSelector` extracts text from record, `vectorSetter` assigns embedding back — decouples from specific property names
- Microsoft.Extensions.VectorData.Abstractions 10.1.0 provides `EmbeddingGenerator` property on collection definition for provider-level integration
- Our implementation provides convenience methods that work with any VectorData provider, not just InMemoryVectorStore
- 22 tests cover all methods, edge cases, null guards, batch operations, and filter integration

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

### 2026-04-04: Embedding Cache and Multi-Model Comparison Tool
- Implemented `CachingEmbeddingDecorator` as an `IEmbeddingGenerator<string, Embedding<float>>` decorator
- Cache uses SHA-256 hash of input text as keys for thread-safe lookup via `ConcurrentDictionary<string, Embedding<float>>`
- LRU eviction policy tracks insertion order using `ConcurrentQueue<string>`, evicting oldest entries when `MaxSize` is reached
- Smart batch handling: checks cache for each input, only generates embeddings for uncached items, then merges results
- `EmbeddingCacheOptions` controls cache behavior: `Enabled` (default: false) and `MaxSize` (default: 10,000)
- Added `AddLocalEmbeddingsWithCache` DI extension that registers both `LocalEmbeddingGenerator` and the optional cache decorator
- Implemented `EmbeddingComparer` for evaluating multiple embedding models on the same dataset
- Comparer computes all pairwise cosine similarities and returns statistics (avg, min, max) per model
- Both implementations follow existing patterns: file-scoped namespaces, XML comments, proper disposal patterns

