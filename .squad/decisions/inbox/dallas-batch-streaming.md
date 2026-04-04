# Batch and Streaming Embeddings API Design

**Date:** 2026-02-13  
**Author:** Dallas (Core Developer)  
**Status:** Implemented

## Decision

Implemented two new extension methods for `IEmbeddingGenerator<string, Embedding<float>>` to support efficient large-scale embedding generation:

1. **Batch API with Progress Reporting**: `GenerateAsync` overload with `IProgress<EmbeddingProgress>` parameter
2. **Streaming API**: `GenerateStreamingAsync` returning `IAsyncEnumerable<Embedding<float>>`

## Rationale

### Design Choices

**Extension Methods vs Direct Implementation:**
- Implemented as extension methods on `IEmbeddingGenerator<string, Embedding<float>>` rather than in `LocalEmbeddingGenerator`
- Keeps the core `LocalEmbeddingGenerator` focused on ONNX inference, not batch orchestration
- Makes these features available to ANY embedding generator implementation, not just ours
- Follows existing pattern established by `GenerateAsync(string)` and `FindClosestAsync` convenience methods

**Progress Record Type:**
- Created `EmbeddingProgress` as a record type with three properties: `CompletedItems`, `TotalItems`, `CurrentBatchSize`
- Record types provide value equality and immutability by default
- Compact syntax matches modern C# patterns
- Easy to extend in future if needed (add timestamp, error count, etc.)

**Batch Size Default:**
- Default batch size of 32 items balances:
  - Memory usage (tokenization + ONNX tensors)
  - Progress reporting granularity
  - ONNX Runtime batch inference efficiency
- Made configurable so users can tune for their specific workloads

**Streaming Design:**
- `IAsyncEnumerable<Embedding<float>>` is the standard .NET pattern for async streaming
- Used `[EnumeratorCancellation]` attribute for proper cancellation token propagation
- Yields embeddings in input order to maintain semantic alignment with original text
- Sequential batch processing (not parallel) to preserve order and avoid memory spikes

**Input Materialization:**
- Both methods call `.ToList()` on input `IEnumerable<string>` upfront
- Required to:
  - Know total count for progress reporting (batch API)
  - Enable efficient `.Chunk(batchSize)` operation
  - Prevent multiple enumeration issues
- Trade-off: upfront memory cost vs enumeration safety

**Cancellation Support:**
- Both methods check `cancellationToken.ThrowIfCancellationRequested()` before each batch
- Enables responsive cancellation on long-running operations
- Combined with `[EnumeratorCancellation]` for streaming API

## Implementation Notes

**File Structure:**
- `EmbeddingProgress.cs` — standalone record type (public API surface)
- Added to existing `EmbeddingGeneratorExtensions.cs` (alongside other convenience methods)
- Required `using System.Runtime.CompilerServices;` for `[EnumeratorCancellation]`

**Error Handling:**
- Validates all arguments with standard .NET patterns
- `ArgumentNullException` for null parameters
- `ArgumentOutOfRangeException` for invalid batch size
- Lets underlying `GenerateAsync` throw its own exceptions (model errors, tokenization failures)

**AOT/Trimming Compatibility:**
- While fixing compilation errors, added required attributes to `ServiceCollectionExtensions.AddLocalEmbeddings(IConfiguration)`
- Used fully qualified names to avoid namespace pollution: `[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode]`

## Alternatives Considered

**IAsyncEnumerable Input:**
- Could have accepted `IAsyncEnumerable<string>` input for streaming API
- Rejected: adds complexity, most users have materialized lists
- Future enhancement if needed

**Parallel Batch Processing:**
- Could process multiple batches in parallel
- Rejected: 
  - Destroys output order (requires complex reordering)
  - Memory spikes from multiple in-flight batches
  - ONNX Runtime already parallelizes internally
  - Sequential is simpler and more predictable

**Built-in to LocalEmbeddingGenerator:**
- Could have added these as instance methods on `LocalEmbeddingGenerator`
- Rejected: extension methods are more composable and work with any `IEmbeddingGenerator` implementation

## Impact

**Benefits:**
- Enables efficient processing of large datasets (1000s+ items)
- Progress reporting for user feedback in long-running operations
- Streaming reduces memory footprint for consumers
- Works with any `IEmbeddingGenerator<string, Embedding<float>>` implementation

**Breaking Changes:**
- None — these are additive features

**Performance:**
- Batch API: overhead is minimal (just progress reporting)
- Streaming API: memory-efficient, no performance penalty vs collecting all results

## Future Enhancements

- Add parallel batch processing option (opt-in via parameter)
- Support `IAsyncEnumerable<string>` input for true streaming pipelines
- Add metrics (tokens/sec, batches/sec) to progress reporting
- Consider batch-level error handling strategy (continue on partial failures)

## Related

- Feature request tracking: roadmap items 1.1 and 1.2
- Complements existing `FindClosestAsync` for semantic search workflows
- Foundation for future RAG pipeline optimizations
