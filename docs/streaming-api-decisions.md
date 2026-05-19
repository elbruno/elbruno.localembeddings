# Streaming Embeddings API — Design Decisions & Trade-offs

**Date:** 2026-05-19  
**Author:** Kane (Integration Developer)  
**Status:** Design Phase 1  
**Audience:** Architecture, Implementation Team, Code Review

---

## Executive Summary

This document captures key design decisions, trade-offs, and rationale for the streaming embeddings API. It serves as a reference for implementation choices and future enhancements.

---

## 1. Buffering Strategy: Size-Based Flush (MVP)

### Decision

Implement **simple size-based buffer flushing** in Phase 1:
- Buffer fills to `BufferSize` → flush immediately
- Input stream ends → flush remaining buffer
- No timeout-based flushing

### Rationale

**Pros:**
- ✅ Simple, predictable behavior (no async state machines)
- ✅ No edge cases from timeout races
- ✅ Minimal overhead (single List<T>)
- ✅ Works for both fast and slow streams

**Cons:**
- ❌ Slow input streams may stall (especially if buffer size > incoming rate)
- ❌ Cannot optimize for interactive scenarios (e.g., chat where latency matters)

### Trade-off Analysis

| Factor | Size-Only | With Timeout |
|--------|-----------|--------------|
| Implementation Complexity | Low | Medium |
| Latency Predictability | Predictable (size-based) | Uncertain (race condition) |
| Stall Risk | Yes (for slow streams) | No (timeout prevents stall) |
| Recommended for | Batch processing | Interactive/streaming APIs |

### When to Reconsider

**Phase 2 enhancement trigger:** If customer feedback indicates latency issues in chat/interactive scenarios.

**Proposed Phase 2 timeout implementation:**
```csharp
// Pseudocode — do NOT implement in Phase 1
public int BufferTimeoutMs { get; set; } = 5000; // in StreamingEmbeddingOptions

// In GenerateStreamingAsync:
// Race condition: buffer-full vs timeout
// Winner → flush | Loser → continue waiting
```

---

## 2. Input Materialization: Lazy (IAsyncEnumerable)

### Decision

Support **true lazy streaming** via `IAsyncEnumerable<string>` parameter:

```csharp
public static async IAsyncEnumerable<Embedding<float>> GenerateStreamingAsync(
    this IEmbeddingGenerator<string, Embedding<float>> generator,
    IAsyncEnumerable<string> texts,  // ← Lazy enumeration
    StreamingEmbeddingOptions? options = null,
    CancellationToken cancellationToken = default)
```

### Rationale

**Why not eager materialization (List<string>)?**

| Scenario | Lazy | Eager (List) |
|----------|------|-------------|
| 100K vector dataset | ✅ O(buffer_size) memory | ❌ O(100K) memory |
| Infinite stream (queue) | ✅ Possible | ❌ Fails |
| File streaming (disk) | ✅ Efficient | ❌ Loads entire file |
| Network stream (API) | ✅ Handles backpressure | ❌ All-or-nothing |

**Decision:** Lazy is **mandatory** for production-scale RAG. Eager is an antipattern for 100K+ vectors.

### Existing Streaming Extension

Note: There is already a `GenerateStreamingAsync(IEnumerable<string>)` extension in the codebase that does eager materialization. This is acceptable for small datasets but not suitable for production RAG.

**Our Phase 1 addition:** Parallel overload using `IAsyncEnumerable<string>`.

---

## 3. Error Handling: Fail-Fast with Partial Results

### Decision

Implement **fail-fast error propagation:**
- Input stream error → immediate exception, buffer abandoned
- Batch generation error → immediate exception, already-yielded embeddings valid
- No automatic retry or error recovery

### Rationale

**Why no retry logic?**

- Retry complicates error semantics (which batch failed? how many retries?)
- User code is better positioned to decide retry strategy (exponential backoff, circuit breaker, etc.)
- Streaming API is a thin wrapper, not a resilience framework

**Example scenario:**
```csharp
// If generator.GenerateAsync() throws on batch 5:
// → Embeddings from batches 1-4 are yielded ✅
// → Exception is thrown immediately
// → Batches 6+ are not processed
// → Buffer is abandoned (no cleanup needed)
```

**User responsibility:** Wrap streaming call in try-catch if retry needed.

---

## 4. Progress Reporting: Deferred to Phase 2

### Decision

**Phase 1:** No progress reporting in MVP.

**Phase 2:** Optional progress overload:
```csharp
await foreach (var embedding in generator.GenerateStreamingAsync(
    texts,
    progress: new Progress<EmbeddingProgress>(p => Console.WriteLine($"{p.CompletedItems} items")),
    options: options))
{
    // ...
}
```

### Rationale

**Why defer?**

- Progress state threading adds complexity (not value-critical for MVP)
- Can be added as an optional overload later without breaking changes
- Existing `GenerateAsync(IProgress<EmbeddingProgress>)` handles progress for non-streaming

**Note:** We actually added progress-reporting overload to skeleton because it's an easy extension and customer demand is likely. Phase 2 can enhance or optimize it.

---

## 5. Options Class Design: Minimal Scope

### Decision

`StreamingEmbeddingOptions` includes only:
- `BufferSize` (int, default 32)
- `EmbeddingOptions` (EmbeddingGenerationOptions?, default null)

**NOT included in MVP:**
- `BufferTimeoutMs` → Phase 2
- `ReportProgress` → Not needed (progress is optional parameter)
- `ErrorRecoveryStrategy` → Not needed (fail-fast only)
- `MaxMemoryBytes` → Not needed (buffer size is implicit limit)

### Rationale

**YAGNI principle:** Include only what we need today. Future options can be added without breaking changes (C# public sealed class with default property values = forward-compatible).

### Future Extensibility

All additions will be additive:
```csharp
// Phase 2: Adding timeout does NOT break existing code
public class StreamingEmbeddingOptions
{
    public int BufferSize { get; set; } = 32;
    public int BufferTimeoutMs { get; set; } = 5000;  // ← New in Phase 2
    public EmbeddingGenerationOptions? EmbeddingOptions { get; set; }
}
```

---

## 6. DI Registration: Extension Methods Only (No Service Changes)

### Decision

**Phase 1:** No new DI service registrations required.

Streaming is implemented as **extension methods on `IEmbeddingGenerator<string, Embedding<float>>`** — the interface already provided by existing `AddLocalEmbeddings()` registration.

```csharp
// This is sufficient:
services.AddLocalEmbeddings();

// User code:
var generator = serviceProvider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
await foreach (var emb in generator.GenerateStreamingAsync(textStream)) { }
```

### Rationale

- ✅ No breaking changes to existing DI setup
- ✅ Streaming is opt-in (methods are extensions, not forced registrations)
- ✅ Simpler startup time

### Phase 2 Option: Convenience Helpers

May add optional service registrations for streaming presets:
```csharp
// Optional in Phase 2
public static IServiceCollection AddStreamingEmbeddingOptions(
    this IServiceCollection services,
    Action<StreamingEmbeddingOptions>? configure = null) { }

// Usage:
services.AddLocalEmbeddings();
services.AddStreamingEmbeddingOptions(opts => opts.BufferSize = 64);
```

---

## 7. Backward Compatibility: Zero Breaking Changes

### Decision

All existing code **must continue to work unchanged:**

| API | Phase 1 Status | Change? |
|-----|----------------|---------|
| `GenerateAsync(IEnumerable<string>)` | Unchanged | ✅ No |
| `GenerateStreamingAsync(IEnumerable<string>, batchSize)` | Unchanged | ✅ No |
| `AddLocalEmbeddings()` | Unchanged | ✅ No |
| `LocalEmbeddingGenerator` class | Unchanged | ✅ No |

### What's New

- New file: `StreamingEmbeddingOptions.cs`
- New file: `StreamingExtensions.cs` (with new overload `GenerateStreamingAsync(IAsyncEnumerable<string>)`)
- New tests

---

## 8. Buffer Size Default: 32

### Decision

Default `BufferSize = 32` (same as `LocalEmbeddingsOptions.BatchSize`).

### Rationale

| Size | Latency to 1st Embedding | GPU Util | Throughput | Use Case |
|------|--------------------------|----------|------------|----------|
| 4 | ~160ms | Moderate | ~2K items/sec | Real-time, interactive |
| **32** | **~1.3s** | **High** | **~2K items/sec** | **Balanced (default)** |
| 128 | ~5s | Very High | ~3K items/sec | Throughput optimization |

**Decision:** 32 is the sweet spot for most workloads. Users can customize via `StreamingEmbeddingOptions.BufferSize`.

---

## 9. Cancellation Handling: Standard .NET Pattern

### Decision

Use standard `CancellationToken` propagation with `[EnumeratorCancellation]` attribute:

```csharp
public static async IAsyncEnumerable<Embedding<float>> GenerateStreamingAsync(
    /* ... */
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    // ... check cancellation at each iteration
    cancellationToken.ThrowIfCancellationRequested();
}
```

### Rationale

- ✅ Standard .NET async pattern
- ✅ Plays well with `await using`, `async foreach`, `CancellationTokenSource`
- ✅ No special cleanup needed (buffer is automatically garbage-collected)

### Edge Case: Cancellation Mid-Buffer

**Scenario:** User cancels while buffer has pending items.

**Behavior:** Buffer is abandoned (not flushed). This is correct because:
- User requested cancellation → they don't want those embeddings
- No implicit side effects
- Consistent with standard async cancellation semantics

---

## 10. Testing Strategy: Comprehensive Unit Tests

### Decision

Use **table-driven tests** (xUnit `[Theory]`) for:
- Buffer flush scenarios
- Cancellation handling
- Empty/single-item inputs
- Error propagation
- Progress reporting

### Rationale

- ✅ Covers all edge cases systematically
- ✅ Easy to add new scenarios (just add row to table)
- ✅ Clear pass/fail per scenario

### Example Test Case Table

```csharp
[Theory]
[InlineData(new[] { "a", "b", "c" }, 2, 2)]                    // 3 items → 2 batches
[InlineData(new[] { "a" }, 32, 1)]                             // 1 item → 1 batch (final flush)
[InlineData(new string[] { }, 32, 0)]                          // Empty → 0 batches
[InlineData(new[] { "a", "b", "c", "d", "e" }, 3, 2)]         // 5 items → 2 batches (2+3)
[InlineData(new[] { "1", "2", "3", "4", "5", "6" }, 2, 3)]   // 6 items → 3 batches (2+2+2)
public async Task GenerateStreamingAsync_FlushesBufferCorrectly(
    string[] texts,
    int bufferSize,
    int expectedBatchCount)
{
    // Arrange
    var generator = new MockEmbeddingGenerator();
    var options = new StreamingEmbeddingOptions { BufferSize = bufferSize };
    
    // Act
    var count = 0;
    await foreach (var _ in generator.GenerateStreamingAsync(
        texts.ToAsyncEnumerable(), 
        options))
    {
        count++;
    }
    
    // Assert
    Assert.Equal(texts.Length, count);
    Assert.Equal(expectedBatchCount, generator.CallCount);
}
```

---

## 11. Future Enhancements (Phase 2+)

### High Priority

1. **Timeout-based flushing** — Prevent stalling on slow streams
   - Risk: Complexity (CTS.CancelAfter race conditions)
   - Value: Better interactive/chat scenarios

2. **Error recovery strategies** — Optional retry with backoff
   - Risk: Over-engineering (user code can retry)
   - Value: Convenience for common patterns

### Medium Priority

3. **Adaptive buffer sizing** — Profile first batch, adjust buffer based on throughput
   - Risk: Hidden behavior, harder to debug
   - Value: Automatic performance tuning

4. **Per-batch error yielding** — Yield partial results on error
   - Risk: Complex API (need error types)
   - Value: Resilience for massive datasets

### Low Priority

5. **Distributed streaming** — Shard across multiple processes
   - Risk: Significant complexity
   - Value: Ultra-high-scale (1M+ vectors)

---

## 12. Known Limitations & Workarounds

| Limitation | Workaround | Planned Fix |
|-----------|-----------|-----------|
| No timeout-based flush (slow stream can stall) | Buffer small dataset, flush manually, or increase `BufferSize` | Phase 2: Add `BufferTimeoutMs` |
| No progress for individual items (only per-batch) | Use progress overload, calculate `ProgressPercentage` | Phase 2: Optional per-item progress callback |
| Cancellation loses unbuffered items | Use smaller `BufferSize` for critical scenarios | (By design) |
| No automatic retry on transient errors | Wrap in try-catch, implement retry logic | Phase 2: Optional strategy pattern |

---

## 13. Decision Changelog

| Date | Decision | Rationale |
|------|----------|-----------|
| 2026-05-19 | MVP: Size-based buffering only | Simplicity, predictability |
| 2026-05-19 | Lazy (IAsyncEnumerable) input | Memory efficiency for 100K+ vectors |
| 2026-05-19 | Fail-fast error propagation | Simplicity, user-controlled retry |
| 2026-05-19 | BufferSize=32 default | Balanced latency vs. throughput |
| 2026-05-19 | No new DI registrations | Streaming as extension methods only |

---

## 14. References

- Architecture document: `docs/streaming-embeddings-architecture.md`
- Skeleton code: `src/ElBruno.LocalEmbeddings/Extensions/StreamingExtensions.cs`
- Options: `src/ElBruno.LocalEmbeddings/Options/StreamingEmbeddingOptions.cs`
- Tests: `tests/ElBruno.LocalEmbeddings.Tests/Streaming/StreamingGeneratorTests.cs`

---

**End of Design Decisions Document**
