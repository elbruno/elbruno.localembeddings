# VectorData Embedding Generation Integration

**By:** Kane (Integration Developer)  
**Date:** 2026-04-04  
**Status:** Implemented  
**Branch:** `squad/update-dependencies-and-roadmap`

## Decision

Implemented text-to-vector search capabilities in the `ElBruno.LocalEmbeddings.VectorData` package through extension methods that integrate `IEmbeddingGenerator<string, Embedding<float>>` with `VectorStoreCollection<TKey, TRecord>`.

## What Was Added

### Extension Methods (`VectorStoreCollectionExtensions`)

1. **SearchByTextAsync** — Converts text query to embedding and searches:
   ```csharp
   var results = await collection.SearchByTextAsync(generator, "laptop computer", top: 5);
   ```

2. **SearchByTextBatchAsync** — Batch text queries with single embedding generation call:
   ```csharp
   var queries = new[] { "laptop", "mouse", "keyboard" };
   var results = await collection.SearchByTextBatchAsync(generator, queries, top: 5);
   ```

3. **UpsertWithEmbeddingAsync** — Auto-embed text content on insert:
   ```csharp
   await collection.UpsertWithEmbeddingAsync(
       generator,
       product,
       p => $"{p.Name} {p.Description}",  // text selector
       (p, embedding) => p.Vector = embedding.Vector);  // vector setter
   ```

4. **UpsertBatchWithEmbeddingAsync** — Batch upsert with automatic embeddings:
   ```csharp
   await collection.UpsertBatchWithEmbeddingAsync(
       generator,
       products,
       p => p.Name,
       (p, e) => p.Vector = e.Vector);
   ```

### Enhanced DI Registration

**AddVectorStoreCollectionWithEmbeddings** — Configures collection with embedding generator:
```csharp
services
    .AddLocalEmbeddingsWithInMemoryVectorStore()
    .AddVectorStoreCollectionWithEmbeddings<int, Product>(
        collectionName: "products",
        useEmbeddingGenerator: true);  // wires IEmbeddingGenerator into collection definition
```

## Design Rationale

### Why Extension Methods vs. Provider Implementation?

- **Provider-agnostic:** Works with any `VectorStoreCollection` implementation (InMemory, Azure, Qdrant, etc.)
- **Composability:** Users can pass decorated/cached generators without modifying collection internals
- **Zero breaking changes:** Extends existing API surface without touching `InMemoryVectorStore` implementation
- **Clear intent:** Method names (`SearchByTextAsync`, `UpsertWithEmbeddingAsync`) make the integration explicit

### Why `textSelector` and `vectorSetter` Callbacks?

- **Decouples from property names:** No reflection, no attribute scanning at runtime
- **Supports complex text:** Can concatenate multiple properties (`$"{p.Name} {p.Description} {p.Category}"`)
- **Type-safe:** Compile-time checks for property access
- **Flexible embedding target:** Can set `ReadOnlyMemory<float>`, `float[]`, or `Embedding<float>` properties

### Why Batch Methods?

- **Performance:** Single `generator.GenerateAsync(texts)` call vs. N individual calls
- **Efficiency:** Reduces ONNX session round-trips when embedding multiple records
- **Mirrors M.E.AI patterns:** Consistent with `IEmbeddingGenerator<TInput, TEmbedding>` batch API

## Integration with Microsoft.Extensions.VectorData 10.1.0

The VectorData 10.1.0 abstraction provides:
- `VectorStoreCollectionDefinition.EmbeddingGenerator` property
- Provider-level automatic embedding for supported stores

Our implementation:
- Complements provider features with universal extension methods
- `AddVectorStoreCollectionWithEmbeddings` sets `definition.EmbeddingGenerator` for providers that use it
- Extension methods work regardless of provider support level

## Testing Coverage

22 tests across two test classes:
- `VectorStoreCollectionExtensionsTests` — 13 tests for extension methods
- `ServiceCollectionExtensionsTests` — updated with 9 tests total (4 new)

Coverage includes:
- Text search with mocked generator
- Batch operations
- Null/empty input validation
- Filter integration
- DI registration with/without generator
- Edge cases (empty collections, empty batches)

## Impact

### For Library Users

**Before:**
```csharp
// Manual embedding generation
var embedding = await generator.GenerateEmbeddingAsync("laptop");
var results = await collection.SearchAsync(embedding, top: 5);
```

**After:**
```csharp
// Direct text search
var results = await collection.SearchByTextAsync(generator, "laptop", top: 5);
```

### For RAG Applications

Batch insertion becomes simpler:
```csharp
// Before: loop + manual embedding
foreach (var doc in documents)
{
    doc.Vector = (await generator.GenerateEmbeddingAsync(doc.Content)).Vector;
}
await collection.UpsertAsync(documents);

// After: single call
await collection.UpsertBatchWithEmbeddingAsync(
    generator,
    documents,
    doc => doc.Content,
    (doc, emb) => doc.Vector = emb.Vector);
```

## Future Enhancements (Out of Scope)

- **Streaming search:** `IAsyncEnumerable<VectorSearchResult<TRecord>> SearchByTextStreamAsync(...)`
- **Progress callbacks:** For large batch operations
- **Hybrid search:** Text + vector in single query (requires VectorData provider support)
- **Automatic caching:** Optional cache integration via decorator pattern

## Related Work

- VectorData package created: 2026-04-04 (roadmap item 4.2)
- Microsoft.Extensions.VectorData.Abstractions updated to 10.1.0: 2026-04-04
- Embedding cache pattern established: 2026-04-04 (PERF audit)

## Open Questions

None — implementation complete and tested.
