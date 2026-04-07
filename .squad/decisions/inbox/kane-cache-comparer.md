# Embedding Cache and Multi-Model Comparison Tool

**Date:** 2026-04-04  
**By:** Kane (Integration Developer)  
**Status:** Implemented

## Context

Implemented two new features for ElBruno.LocalEmbeddings roadmap items 1.4 and 1.5:
- In-memory LRU embedding cache
- Multi-model embedding comparison tool

## Technical Decisions

### 1.4: Embedding Cache (CachingEmbeddingDecorator)

**Pattern:** Decorator implementing `IEmbeddingGenerator<string, Embedding<float>>`

**Key Design Choices:**
1. **Cache Key:** SHA-256 hash of input text (using `System.Security.Cryptography.SHA256.HashData`)
   - Ensures consistent keys regardless of string encoding variations
   - Compact string representation via `Convert.ToHexString`

2. **LRU Eviction:** 
   - `ConcurrentDictionary<string, Embedding<float>>` for thread-safe storage
   - `ConcurrentQueue<string>` for insertion order tracking
   - Lock-based eviction in `EvictOldest()` to maintain size limit

3. **Smart Batch Handling:**
   - Checks cache for each input separately
   - Only sends uncached items to inner generator
   - Merges cached and newly-generated results maintaining input order
   - Preserves usage metadata from inner generator

4. **Disposal:** Implements both `IDisposable` and `IAsyncDisposable`
   - Properly disposes inner generator
   - Clears cache on disposal

5. **DI Integration:**
   - `AddLocalEmbeddingsWithCache` registers both base generator and optional cache decorator
   - Cache only applied when `EmbeddingCacheOptions.Enabled = true`
   - Default: cache disabled (backward compatible, opt-in)

### 1.5: Multi-Model Comparison Tool (EmbeddingComparer)

**Pattern:** Standalone utility class for model evaluation

**Key Design Choices:**
1. **Constructor Injection:** Takes collection of `(string Name, IEmbeddingGenerator)` tuples
   - Allows explicit naming or falls back to `metadata.DefaultModelId`

2. **Pairwise Similarities:**
   - Computes all unique pairs (i, j) where i < j
   - For n texts, produces n*(n-1)/2 similarity scores
   - Uses existing `EmbeddingExtensions.CosineSimilarity` method

3. **Statistics:** Returns min, max, average similarity per model
   - Full pairwise list included for detailed analysis

4. **Records for Results:**
   - `ModelComparisonResult` - per-model statistics
   - `ComparisonReport` - full report across all models
   - Immutable, clean API surface

## Configuration

### EmbeddingCacheOptions
```csharp
public sealed class EmbeddingCacheOptions
{
    public bool Enabled { get; set; }           // Default: false
    public int MaxSize { get; set; } = 10_000;  // Default: 10,000
}
```

### DI Registration
```csharp
services.AddLocalEmbeddingsWithCache(
    configureEmbeddings: opts => opts.ModelName = "...",
    configureCache: opts => { 
        opts.Enabled = true; 
        opts.MaxSize = 5000; 
    });
```

## Files Created

- `src/ElBruno.LocalEmbeddings/Options/EmbeddingCacheOptions.cs`
- `src/ElBruno.LocalEmbeddings/CachingEmbeddingDecorator.cs`
- `src/ElBruno.LocalEmbeddings/EmbeddingComparer.cs`

## Files Modified

- `src/ElBruno.LocalEmbeddings/Extensions/ServiceCollectionExtensions.cs`
  - Added `AddLocalEmbeddingsWithCache` method

## Rationale

1. **Decorator Pattern:** Follows M.E.AI patterns and allows cache to be composed with any `IEmbeddingGenerator`
2. **SHA-256 Hashing:** Provides consistent, collision-resistant keys without exposing raw text in memory
3. **Opt-in Cache:** Avoids unexpected memory consumption; users explicitly enable when beneficial
4. **LRU Eviction:** Simple, predictable memory bounds; more sophisticated policies (LFU, ARC) deferred
5. **Separate Comparer Class:** Keeps evaluation logic independent of core generator; useful for benchmarking and model selection

## Future Considerations

- Persistent cache options (SQLite, binary format) - deferred per roadmap
- Cache statistics/metrics (hit rate, eviction count) - nice-to-have
- More sophisticated eviction policies (LFU, adaptive) - performance optimization opportunity
- Async eviction to reduce lock contention - if profiling shows bottleneck
