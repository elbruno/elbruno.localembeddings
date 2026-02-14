# Decision: InMemoryVectorStore Pattern for Samples

**Author:** Kane (Integration Developer)  
**Date:** 2026-02-12  
**Status:** Implemented

## Context

When building RAG-style samples that need vector storage, we needed a pattern that:
- Demonstrates proper DI integration with M.E.AI abstractions
- Is simple enough for sample code yet realistic
- Shows batch embedding generation with progress feedback

## Decision

Created `InMemoryVectorStore` that takes `IEmbeddingGenerator<string, Embedding<float>>` via constructor injection:

```csharp
public sealed class InMemoryVectorStore
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    
    public InMemoryVectorStore(IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
    {
        _embeddingGenerator = embeddingGenerator ?? throw new ArgumentNullException(nameof(embeddingGenerator));
    }
}
```

Key API patterns:
- `AddDocumentsAsync()` with `Action<int, int>? progressCallback` for batch loading with progress
- `SearchAsync()` returns `List<SearchResult>` with document and similarity score
- Uses cosine similarity for relevance ranking

## Rationale

- Constructor injection allows clean registration: `services.AddSingleton<InMemoryVectorStore>()`
- Progress callback pattern avoids coupling to specific UI frameworks
- SearchResult as separate type keeps Document clean and allows future metadata expansion
