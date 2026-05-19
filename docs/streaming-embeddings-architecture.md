# Streaming Embeddings API Architecture

**Status:** Design Specification (Phase 1 Prototype)  
**Author:** Kane (Integration Developer)  
**Date:** 2026-05-19  
**Priority:** HIGH — Unblocks Phase 2 full implementation for production-scale RAG

---

## 1. Overview

This document defines the architecture for a production-scale streaming embeddings API that enables incremental embedding generation for 100K+ vectors without exhausting memory or blocking threads. The design maintains full backward compatibility with the existing batch API while adding a composable streaming layer.

**Key Constraint:** The M.E.AI `IEmbeddingGenerator<string, Embedding<float>>` interface does not define streaming methods, so streaming is implemented as extension methods that wrap the core `GenerateAsync()` batch operation.

---

## 2. Design Goals

1. ✅ **Streaming Support:** Accept `IAsyncEnumerable<string>` input, emit `IAsyncEnumerable<Embedding<float>>` output
2. ✅ **Backward Compatibility:** Existing batch APIs remain unchanged and untouched
3. ✅ **Internal Batching:** Buffer incoming texts to optimal batch size, flush on stream end or timeout
4. ✅ **M.E.AI Integration:** Work seamlessly with `IEmbeddingGenerator<string, Embedding<float>>`
5. ✅ **Non-Breaking DI:** New service registrations are additive (no API modifications)
6. ✅ **Cancellation Support:** Full `CancellationToken` propagation and cleanup
7. ✅ **Error Handling:** Distinguish between recoverable batch errors and unrecoverable stream errors

---

## 3. Interface Design

### 3.1 Current Streaming Extension (Baseline)

The library already provides basic streaming via `EmbeddingGeneratorExtensions.GenerateStreamingAsync()`:

```csharp
public static async IAsyncEnumerable<Embedding<float>> GenerateStreamingAsync(
    this IEmbeddingGenerator<string, Embedding<float>> generator,
    IEnumerable<string> values,
    int batchSize = 32,
    EmbeddingGenerationOptions? options = null,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
```

**Characteristics:**
- Takes `IEnumerable<string>` (eager materialization to list)
- Chunks input into batches, processes sequentially
- Yields embeddings as each batch completes
- Simple, predictable semantics
- **Limitation:** Requires loading entire input list into memory upfront

### 3.2 Enhanced Streaming Interface (New)

For true streaming semantics with async input enumeration:

```csharp
public static async IAsyncEnumerable<Embedding<float>> GenerateStreamingAsync(
    this IEmbeddingGenerator<string, Embedding<float>> generator,
    IAsyncEnumerable<string> texts,
    StreamingEmbeddingOptions? options = null,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
```

**Key Differences:**
- Accepts `IAsyncEnumerable<string>` (true async streaming)
- Buffers incoming texts incrementally (configurable buffer size)
- Processes buffer on size threshold or timeout
- Never forces full input materialization
- Handles mid-buffer cancellation gracefully

### 3.3 Supporting Options Type

```csharp
/// <summary>
/// Configuration for streaming embedding generation.
/// </summary>
public sealed class StreamingEmbeddingOptions
{
    /// <summary>
    /// Buffer size for batching incoming texts before processing.
    /// Default: 32 (matches default batch size).
    /// </summary>
    /// <remarks>
    /// Larger buffers → better GPU utilization but higher memory.
    /// Smaller buffers → lower latency but more inference calls.
    /// </remarks>
    public int BufferSize { get; set; } = 32;

    /// <summary>
    /// Maximum time to wait for buffer to fill before flushing.
    /// Default: 5000ms (5 seconds).
    /// </summary>
    /// <remarks>
    /// Prevents stalling on slow input streams.
    /// Set to Timeout.Infinite to disable timeout (flush only on buffer full or stream end).
    /// </remarks>
    public int BufferTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Whether to report per-embedding progress.
    /// Default: false.
    /// </summary>
    /// <remarks>
    /// When enabled, the method can report progress via a progress reporter.
    /// (See GenerateStreamingWithProgressAsync overload.)
    /// </remarks>
    public bool ReportProgress { get; set; } = false;

    /// <summary>
    /// Underlying embedding generation options passed to each batch.
    /// Default: null (uses generator defaults).
    /// </summary>
    public EmbeddingGenerationOptions? EmbeddingOptions { get; set; }
}
```

---

## 4. Implementation Strategy

### 4.1 Buffer-Based Batching Architecture

```
Input Stream (IAsyncEnumerable<string>)
        ↓
   ┌─────────────────────┐
   │  Buffering Logic    │ (StreamingBufferQueue)
   │                     │
   │ - Buffer size: N    │ ← Configurable (default 32)
   │ - Timeout: T ms     │ ← Configurable (default 5s)
   │ - Current fill: K   │
   └─────────────────────┘
        ↓
   ┌─────────────────────┐
   │ Flush Trigger       │ (When K == N OR timeout OR stream end)
   └─────────────────────┘
        ↓
   ┌─────────────────────┐
   │ Batch Generation    │ (generator.GenerateAsync(batch))
   │ (ONNX Inference)    │
   └─────────────────────┘
        ↓
   Output Stream (IAsyncEnumerable<Embedding<float>>)
   (Yields embeddings in input order)
```

### 4.2 Core Algorithm Pseudocode

```csharp
async IAsyncEnumerable<Embedding<float>> GenerateStreamingAsync(
    IAsyncEnumerable<string> texts,
    StreamingEmbeddingOptions options = default,
    CancellationToken ct = default)
{
    var buffer = new List<string>(capacity: options.BufferSize);
    var bufferTimeoutMs = options.BufferTimeoutMs;
    
    // Create a cancellation token that includes both user ct and timeout
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    
    try {
        // Enumerate input stream
        await foreach (var text in texts.WithCancellation(ct)) {
            ct.ThrowIfCancellationRequested();
            
            // Add to buffer
            buffer.Add(text);
            
            // Flush when buffer is full
            if (buffer.Count >= options.BufferSize) {
                var batch = buffer.ToList();
                buffer.Clear();
                
                // Generate embeddings for this batch
                var embeddings = await generator.GenerateAsync(
                    batch, 
                    options.EmbeddingOptions, 
                    ct);
                
                // Yield embeddings in order
                foreach (var embedding in embeddings) {
                    yield return embedding;
                }
            }
            // Optional: Trigger timeout-based flush here (see 4.3)
        }
        
        // Stream ended: flush remaining buffer
        if (buffer.Count > 0) {
            var embeddings = await generator.GenerateAsync(
                buffer,
                options.EmbeddingOptions,
                ct);
            
            foreach (var embedding in embeddings) {
                yield return embedding;
            }
        }
    }
    catch (OperationCanceledException ex) {
        // Handle cancellation (see 6.2)
        // Clean up resources if needed
        throw;
    }
    catch (Exception ex) {
        // Handle batch generation failure (see 6.3)
        throw;
    }
}
```

### 4.3 Timeout-Based Flush (Optional Enhancement)

For low-throughput streams where buffer might not fill, implement timeout-based flushing:

```csharp
async Task<bool> WaitForBufferOrTimeout(
    List<string> buffer,
    IAsyncEnumerator<string> enumerator,
    int bufferSize,
    int timeoutMs,
    CancellationToken ct)
{
    // If buffer is full, don't wait
    if (buffer.Count >= bufferSize) return true;
    
    // If timeout disabled, wait indefinitely
    if (timeoutMs == Timeout.Infinite) {
        return await enumerator.MoveNextAsync();
    }
    
    // Race: buffer fill vs timeout
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    cts.CancelAfter(timeoutMs);
    
    try {
        buffer.Add(enumerator.Current);
        if (buffer.Count >= bufferSize) return true; // Buffer full
        
        // Continue reading until timeout or buffer full
        while (await enumerator.MoveNextAsync()) {
            buffer.Add(enumerator.Current);
            if (buffer.Count >= bufferSize) return true;
        }
        return false; // Stream ended
    }
    catch (OperationCanceledException) {
        // Timeout reached
        return buffer.Count > 0; // Flush if we have items
    }
}
```

**Implementation Note:** Full timeout logic deferred to Phase 2. MVP uses size-based flushing only.

---

## 5. Dependency Injection Registration

### 5.1 No Modifications to Existing Services

The streaming methods are **extension methods only** — they do not require new DI registrations or service types.

Existing registrations remain unchanged:
```csharp
services.AddLocalEmbeddings();  // Already supports streaming via extensions
```

### 5.2 Optional: Streaming Presets

For convenience, provide helper extensions for common streaming patterns:

```csharp
/// <summary>
/// Extension method to add streaming embedding presets to DI.
/// </summary>
public static class StreamingServiceCollectionExtensions
{
    /// <summary>
    /// Adds a factory for creating streaming embedding options with sensible defaults.
    /// </summary>
    public static IServiceCollection AddStreamingEmbeddingOptions(
        this IServiceCollection services,
        Action<StreamingEmbeddingOptions>? configure = null)
    {
        services.AddOptions<StreamingEmbeddingOptions>();
        if (configure is not null) {
            services.Configure(configure);
        }
        return services;
    }
}
```

**Usage:**
```csharp
services.AddLocalEmbeddings();
services.AddStreamingEmbeddingOptions(options => {
    options.BufferSize = 64;
    options.BufferTimeoutMs = 3000;
});

// In application code:
var streamingOptions = serviceProvider.GetRequiredService<IOptions<StreamingEmbeddingOptions>>().Value;
await foreach (var embedding in generator.GenerateStreamingAsync(textStream, streamingOptions)) {
    // Process embedding
}
```

---

## 6. Error Handling & Edge Cases

### 6.1 Input Stream Errors

**Scenario:** Input enumeration fails (network error, cancellation, etc.)

**Behavior:**
- Exception propagates immediately
- Remaining buffer is **not** flushed
- Rationale: User code is responsible for handling stream errors

**Example:**
```csharp
async IAsyncEnumerable<string> FetchTextsFromApi() {
    // If API call fails mid-enumeration, exception is thrown
    yield return "text1";
    yield return "text2";
    throw new HttpRequestException("Network error");
    // Buffer at this point is NOT flushed
}

await foreach (var emb in generator.GenerateStreamingAsync(FetchTextsFromApi())) {
    // Exception thrown before reaching here
}
```

### 6.2 Batch Generation Failures

**Scenario:** `generator.GenerateAsync(batch)` throws

**Behavior:**
- Exception propagates immediately (wrapped in `AggregateException` if needed)
- Already-yielded embeddings are safe
- Remaining input is **not** processed
- Rationale: Simplifies error handling; user can retry or abandon

**Decision:** Do NOT implement automatic retry logic. Streaming API is a thin wrapper.

### 6.3 Cancellation Handling

**Scenario:** `CancellationToken` is signaled mid-stream

**Behavior:**
- `OperationCanceledException` is thrown
- Current batch in-flight may complete or be aborted (depends on generator implementation)
- Already-yielded embeddings remain valid
- Buffer is abandoned (not flushed)

**Implementation:**
```csharp
try {
    await foreach (var text in texts.WithCancellation(ct)) {
        ct.ThrowIfCancellationRequested(); // Check at each iteration
        buffer.Add(text);
        // ...
    }
} catch (OperationCanceledException) {
    // Clean up if needed (buffer is already a List<T>, no explicit cleanup needed)
    throw;
}
```

### 6.4 Edge Cases

| Scenario | Behavior | Rationale |
|----------|----------|-----------|
| Empty input stream | Returns empty async enumerable (no yields) | Mirrors batch API behavior |
| Single item (buffer size > 1) | Yields 1 embedding after stream ends | Flush remaining buffer |
| Buffer size = 1 | Yields embedding after each item (no batching) | Valid but inefficient |
| Buffer size > input count | Processes all in single batch after stream ends | Normal case |
| Cancellation mid-buffer | Exception thrown, buffer abandoned | User can catch and handle |
| Null options | Uses `StreamingEmbeddingOptions` defaults | Follows M.E.AI convention |

---

## 7. Performance Characteristics

### 7.1 Memory Profile

- **Input buffer:** ~N text strings (N = buffer size, e.g., 32)
- **Tokenization buffer:** ~N sequences of tokens (ONNX tokenizer internal)
- **ONNX session buffer:** Constant (model weight + intermediate tensors)
- **Output buffer:** 0 (embeddings yielded immediately, not accumulated)

**Total Memory:** O(buffer_size + model_size), independent of total input size ✅

### 7.2 Latency Profile

For a stream of 100K texts with buffer size 32:

```
Time = (100_000 / 32) × T_batch + T_stream_overhead
     ≈ 3,125 batches × 50ms + ~10ms
     ≈ 156 seconds (for typical 50ms batch inference)

Yielding starts after: (buffer_size) × (avg_fetch_latency)
```

**Streaming latency:** First embedding yielded after ~32 items fetched (not after all 100K).

### 7.3 Throughput

Expected throughput improvement vs. single-item processing:

| Scenario | Batches | Est. Throughput |
|----------|---------|-----------------|
| Single-item (batch_size=1) | 100,000 | ~20 items/sec (overhead-limited) |
| Streaming (batch_size=32) | 3,125 | ~2,000 items/sec (batching speedup) |
| Batch API (batch_size=32) | 3,125 | ~2,000 items/sec (same as streaming) |

---

## 8. Design Decisions & Trade-offs

### Decision 1: Buffer Size Tradeoff

**Choice:** Configurable buffer size (default 32)

| Factor | Small Buffer (4) | Medium Buffer (32) | Large Buffer (128) |
|--------|-----|--------|-------|
| Latency to first embedding | ~100ms | ~800ms | ~3.2s |
| GPU utilization | Moderate | High | Very High |
| Memory overhead | Low | Medium | High |
| Recommended for | Real-time | Batch processing | Throughput optimization |

**Decision:** Default 32 aligns with existing `LocalEmbeddingsOptions.BatchSize`.

### Decision 2: Eager vs. Lazy Input Materialization

**Choice:** Lazy (true streaming via `IAsyncEnumerable<string>`)

- ✅ No upfront memory cost for large inputs
- ✅ Can process infinite streams (e.g., message queue)
- ✅ Better for low-throughput, long-lived connections

**Alternative:** Eager (convert to `List<string>` first)
- ✗ Simpler implementation
- ✗ Fails for infinite streams
- ✗ Memory explosion for 100K+ vectors

**Chosen:** Lazy (via `GenerateStreamingAsync(IAsyncEnumerable<string>)` overload)

### Decision 3: Timeout-Based Flushing

**Choice:** Deferred to Phase 2 (MVP omits timeout logic)

**Rationale:**
- Adds complexity (Task.WhenAny, CTS.CancelAfter, etc.)
- Most streams are either:
  - Fast (buffer fills quickly) → No timeout needed
  - Slow + unbounded (perfect for batch processing) → User controls timing
- Timeout helps interactive/UI scenarios; deferred for now

**Phase 2 Enhancement:** Add `BufferTimeoutMs` to `StreamingEmbeddingOptions` with timeout-based flush.

### Decision 4: Buffering Strategy

**Choice:** Simple in-memory list + size-based flush

**Alternatives Considered:**
- **Channels (`System.Threading.Channels`):** Better for producer-consumer; overkill for this use case
- **Priority queue (time-based flush):** Complexity not justified in MVP
- **Adaptive batching:** Monitor throughput; adjust buffer size → too complex

**Chosen:** List-based for clarity, simplicity, and compatibility with existing code.

### Decision 5: Progress Reporting

**Choice:** Not in MVP; deferred to Phase 2

**Why:**
- Requires threading progress state through the async enumerable
- Can use `IProgress<StreamingEmbeddingProgress>` in future overload
- Not critical for Phase 1

---

## 9. Backward Compatibility

### No Breaking Changes

✅ Existing batch API unchanged:
```csharp
await generator.GenerateAsync(new[] { "text1", "text2" })
```

✅ Existing streaming extension works as-is:
```csharp
await foreach (var emb in generator.GenerateStreamingAsync(texts, batchSize: 32)) { }
```

✅ Existing DI registrations unchanged:
```csharp
services.AddLocalEmbeddings()
```

### New Capability (Additive)

✅ New overload for true async streaming:
```csharp
await foreach (var emb in generator.GenerateStreamingAsync(asyncTexts, options)) { }
```

✅ New options type:
```csharp
var options = new StreamingEmbeddingOptions { BufferSize = 64 };
```

---

## 10. Implementation Roadmap

### Phase 1 (MVP) — This Sprint

**Deliverables:**
1. `StreamingEmbeddingOptions` class
2. `GenerateStreamingAsync(IAsyncEnumerable<string>)` extension method
3. Architecture documentation ✅ (this file)
4. Unit tests (table-driven)

**Scope:**
- Size-based buffering (no timeout)
- Simple error propagation
- Full `CancellationToken` support

**File Location:**
- `src/ElBruno.LocalEmbeddings/Streaming/StreamingEmbeddingOptions.cs` (new)
- `src/ElBruno.LocalEmbeddings/Extensions/StreamingExtensions.cs` (new)
- `tests/ElBruno.LocalEmbeddings.Tests/Streaming/StreamingGeneratorTests.cs` (new)

### Phase 2 (Enhancement)

**Proposed Additions:**
1. Timeout-based flush logic
2. Progress reporting (`IProgress<StreamingEmbeddingProgress>`)
3. Adaptive buffer sizing
4. Per-batch error handling (yield partial results)
5. Streaming service registration helpers

### Phase 3 (Advanced)

**Future Directions:**
1. Streaming with custom deserializers (JSON lines, Parquet, etc.)
2. Parallel batch processing (multiple ONNX sessions)
3. Distributed streaming (sharding across processes)

---

## 11. Pseudo-code Example: Phase 1 Implementation

### StreamingEmbeddingOptions.cs

```csharp
namespace ElBruno.LocalEmbeddings.Options;

/// <summary>
/// Configuration for streaming embedding generation.
/// </summary>
public sealed class StreamingEmbeddingOptions
{
    /// <summary>
    /// Buffer size for batching incoming texts. Default: 32.
    /// </summary>
    public int BufferSize { get; set; } = 32;

    /// <summary>
    /// Underlying embedding generation options. Default: null.
    /// </summary>
    public EmbeddingGenerationOptions? EmbeddingOptions { get; set; }
}
```

### StreamingExtensions.cs (New)

```csharp
namespace ElBruno.LocalEmbeddings.Extensions;

using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

/// <summary>
/// Extension methods for streaming embedding generation.
/// </summary>
public static class StreamingExtensions
{
    /// <summary>
    /// Generates embeddings for an async stream of texts with buffering.
    /// </summary>
    public static async IAsyncEnumerable<Embedding<float>> GenerateStreamingAsync(
        this IEmbeddingGenerator<string, Embedding<float>> generator,
        IAsyncEnumerable<string> texts,
        StreamingEmbeddingOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(texts);

        var opts = options ?? new StreamingEmbeddingOptions();
        
        if (opts.BufferSize < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "BufferSize must be greater than zero.");
        }

        var buffer = new List<string>(capacity: opts.BufferSize);

        try
        {
            // Enumerate input stream and buffer
            await foreach (var text in texts.WithCancellation(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                buffer.Add(text);

                // Flush when buffer is full
                if (buffer.Count >= opts.BufferSize)
                {
                    var batch = buffer.ToList();
                    buffer.Clear();

                    var embeddings = await generator.GenerateAsync(
                        batch,
                        opts.EmbeddingOptions,
                        cancellationToken).ConfigureAwait(false);

                    foreach (var embedding in embeddings)
                    {
                        yield return embedding;
                    }
                }
            }

            // Flush remaining buffer
            if (buffer.Count > 0)
            {
                var embeddings = await generator.GenerateAsync(
                    buffer,
                    opts.EmbeddingOptions,
                    cancellationToken).ConfigureAwait(false);

                foreach (var embedding in embeddings)
                {
                    yield return embedding;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Propagate cancellation
            throw;
        }
    }
}
```

---

## 12. Testing Strategy

### Unit Test Cases (Table-Driven)

```csharp
[Theory]
[InlineData(new[] { "a", "b", "c" }, 2, 2)] // 3 items, buffer=2 → 2 batches
[InlineData(new[] { "a" }, 32, 1)]          // 1 item, buffer=32 → 1 batch
[InlineData(new string[] { }, 32, 0)]       // Empty stream → 0 batches
[InlineData(new[] { "a", "b", "c", "d", "e" }, 3, 2)] // 5 items, buffer=3 → 2 batches
public async Task GenerateStreamingAsync_ProducesCorrectBatches(
    string[] texts,
    int bufferSize,
    int expectedBatchCount)
{
    // Arrange
    var generator = new MockEmbeddingGenerator();
    var options = new StreamingEmbeddingOptions { BufferSize = bufferSize };
    
    // Act
    var embeddings = new List<Embedding<float>>();
    await foreach (var emb in generator.GenerateStreamingAsync(texts.ToAsyncEnumerable(), options))
    {
        embeddings.Add(emb);
    }
    
    // Assert
    Assert.Equal(texts.Length, embeddings.Count);
    Assert.Equal(expectedBatchCount, generator.BatchCallCount);
}

[Fact]
public async Task GenerateStreamingAsync_ThrowsOnCancellation()
{
    // Arrange
    var generator = new LocalEmbeddingGenerator();
    var cts = new CancellationTokenSource();
    cts.CancelAfter(100);
    
    // Act & Assert
    await Assert.ThrowsAsync<OperationCanceledException>(
        async () =>
        {
            await foreach (var _ in generator.GenerateStreamingAsync(
                ProduceInfiniteStream(),
                cancellationToken: cts.Token))
            {
                // ...
            }
        });
}
```

---

## 13. References

- **M.E.AI:** https://github.com/dotnet/extensions/tree/main/src/Libraries/Microsoft.Extensions.AI
- **ONNX Runtime:** https://onnxruntime.ai/docs/
- **IAsyncEnumerable:** https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.iasyncenumerable-1

---

## 14. Appendix: Related Documentation

- `docs/getting-started.md` — Quick start guide
- `docs/configuration.md` — Embedding generation options
- `docs/dependency-injection.md` — DI patterns
- `docs/api-reference.md` — Full API documentation

---

**End of Architecture Document**
