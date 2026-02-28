# Parker — History & Learnings

## Project Context

- **Project:** ElBruno.LocalEmbeddings — a .NET library for local embedding generation using Microsoft.Extensions.AI and ONNX Runtime
- **Owner:** Bruno Capuano
- **Stack:** .NET 8.0 / 10.0 (multi-target), C#, Microsoft.Extensions.AI, ONNX Runtime, HuggingFace models, NuGet package distribution
- **Joined:** 2026-02-28

## Key Performance Concerns for This Project

1. **ONNX inference throughput:** The primary bottleneck. Session options (inter/intra op thread counts, execution provider selection) can have large impact.
2. **Model loading latency:** First-call cost from loading and JIT-compiling the ONNX model. Worth measuring cold vs. warm.
3. **Tokenization hot path:** Tokenizer runs per input string — allocation pressure here compounds at scale.
4. **Embedding normalization:** TensorPrimitives is already used for SIMD-accelerated cosine similarity and L2 normalization — confirm all vector math goes through this path.
5. **PreferQuantized model selection:** Quantized/int8 models are smaller and faster for inference — ensure this is the default and benchmark the difference.
6. **Batch embedding:** Single vs. batched inference throughput difference is significant — worth exposing and benchmarking.
7. **NuGet package footprint:** Library consumers care about startup time and package size — track dependency weight.

## Learnings

<!-- Append new learnings here as work progresses -->

### 2025-07-16: Phase 3 Memory & Search Optimizations Implemented

**PERF-09 — PriorityQueue min-heap in EmbeddingExtensions.FindClosest:**
- Both `FindClosest<T>` and `FindClosest(Embedding, IReadOnlyList)` overloads replaced LINQ `OrderByDescending().Take()` with a `PriorityQueue<TElement, float>` min-heap of capacity `topK`.
- Complexity improves from O(n log n) to O(n log k); at large corpus sizes with small topK this is a significant win.
- `PriorityQueue.TryPeek(out _, out float lowestScore)` is the correct way to inspect the minimum priority — `Peek()` returns the element, not the priority.
- `DequeueEnqueue` is available in .NET 8+ (safe since the project targets net8.0 and net10.0).
- The second `FindClosest` overload preserves the `ThenBy(index)` tiebreaker by sorting the final topK results list after heap extraction — cost is O(k log k) on at most topK items.

**PERF-10 — PriorityQueue min-heap in ImageSearchEngine.RankResults:**
- Replaced the two-phase approach (accumulate all results into a List, then sort+take) with a single-pass min-heap.
- Eliminates the intermediate `List<(string, float)>` whose size scales with corpus (n entries allocated, most discarded).

**PERF-06/07 — Direct tensor access in ClipImageEncoder and ClipTextEncoder:**
- Replaced `results.First().AsEnumerable<float>().ToArray()` with `results.First().AsTensor<float>().ToArray()`.
- `AsTensor<T>()` returns the underlying `Tensor<T>` (a `DenseTensor<T>` for ORT inference results), and `.ToArray()` on it copies directly from the backing buffer, bypassing IEnumerable iterator overhead.

**PERF-08 — Eliminated intermediate int[] in Tokenizer.Tokenize:**
- `_tokenizer.EncodeToIds()` returns `IReadOnlyList<int>`. The original code called `.ToArray()` on it to get an `int[]`, then converted element-by-element to `long[]` in a for loop — allocating an entire array that was immediately discarded.
- Fix: iterate `encoding` directly (it's already indexable as `IReadOnlyList<int>`), writing to `long[] inputIds` in the same loop. One fewer heap allocation per `Tokenize` call; at batch=100 this removes 100 `int[]` allocations per inference call.

**PERF-12/13 — Removed redundant .ToList() calls:**
- `LocalEmbeddingGenerator.GenerateAsync`: removed `.ToList()` on the `rawEmbeddings.Select(...)` passed to `GeneratedEmbeddings<T>` constructor (it accepts `IEnumerable<T>`), saving one intermediate list allocation per inference call.
- `Tokenizer.TokenizeBatch`: changed `texts.ToList()` to `texts as IList<string> ?? texts.ToList()`. Since `GenerateAsync` always passes a `List<string>`, the `as` cast avoids re-allocating a duplicate list for every batch call.

**Build result:** `dotnet build` succeeded with 0 warnings, 0 errors.
**Test result:** All 396 tests passed across both target frameworks (net8.0 + net10.0), 10 skipped (require real CLIP model files).

### 2025-07-16: Phase 2 Performance Fixes Implemented

**PERF-03 — SessionOptions disposal on success path (OnnxEmbeddingModel):**
- The original code had two separate try/catch blocks: one to create `SessionOptions`, one to create `InferenceSession`. The `sessionOptions.Dispose()` was only called in the `InferenceSession` catch block — the success path leaked the object.
- Fix: collapsed both try blocks into a single `try` wrapping a `using var sessionOptions`, so disposal is guaranteed in all paths. ORT copies session options during `InferenceSession` construction, making post-construction disposal safe.

**PERF-15/16 — Optimized SessionOptions for CLIP encoders (ClipImageEncoder, ClipTextEncoder):**
- Both CLIP encoders previously called `new InferenceSession(modelPath)` with no options — using ORT defaults (no graph optimization, default threading).
- Applied the same optimized pattern used in `OnnxEmbeddingModel`: `GraphOptimizationLevel.ORT_ENABLE_ALL`, `ExecutionMode.ORT_SEQUENTIAL` (CLIP models are used per-input, not in large batches), `InterOpNumThreads = 1`, `IntraOpNumThreads = Environment.ProcessorCount`.
- Used `using var sessionOptions` to ensure disposal after `InferenceSession` is constructed.

**Build result:** `dotnet build` succeeded with no errors or new warnings.

### 2025-07-16: Phase 1 Performance Fixes Implemented

Implemented the two HIGH-impact fixes from the audit directly in `src/ElBruno.LocalEmbeddings/OnnxEmbeddingModel.cs`.

**PERF-02 — Mean pooling SIMD (TensorPrimitives):**
- Replaced the triple nested scalar loop (`batch × seq × hidden`) with a two-level loop that uses `TensorPrimitives.Add` for the hidden-dimension accumulation and `TensorPrimitives.Divide` for the final normalization.
- Key technique: cast `Tensor<float>` to `DenseTensor<float>` (safe — ORT always returns DenseTensor), take `.Buffer.Span`, then compute the flat offset as `(batch * sequenceLength + seq) * hiddenSize` to get a contiguous `hiddenSize`-length slice per token.
- Attention mask handled by skipping (`continue`) zero-mask tokens rather than multiplying — avoids a wasted SIMD add of zeros and keeps the token count simple as `int`.

**PERF-01 — ArrayPool for flattening arrays:**
- Replaced `new long[batchSize * sequenceLength]` for `flatInputIds`, `flatAttentionMask`, and `flatTokenTypeIds` with `ArrayPool<long>.Shared.Rent(totalSize)` / `Return` in a `try/finally`.
- Rented arrays sliced to exact size via `.AsMemory(0, totalSize)` when constructing `DenseTensor<long>` — required because rented arrays may be larger than requested.
- `flatTokenTypeIds` explicitly cleared with `.AsSpan(0, totalSize).Clear()` since `ArrayPool` does not zero-initialize.
- Removed the unused `shape` variable that preceded tensor construction.

**Build result:** `dotnet build` succeeded with no errors or new warnings.

### 2025-07-16: Comprehensive Performance Audit Completed

Performed full-codebase performance audit covering all 5 source projects. Key findings:

**What's working well:**
- ONNX sessions are reused (singleton pattern), thread-safe for concurrent inference
- TensorPrimitives used for cosine similarity and L2 normalization (SIMD-accelerated)
- Batched inference is supported in OnnxEmbeddingModel.GenerateEmbeddings
- ConfigureAwait(false) used consistently across all async methods
- Graph optimization level set to ORT_ENABLE_ALL
- Benchmark project exists (samples/BenchmarkSample) with 3 benchmark classes

**Critical findings (17 total, 2 HIGH, 12 MEDIUM, 3 LOW):**
1. HIGH: Mean pooling in OnnxEmbeddingModel uses scalar element-by-element loop instead of SIMD/TensorPrimitives — biggest throughput improvement opportunity
2. HIGH: 3 large long[] arrays allocated per GenerateEmbeddings call without ArrayPool — 1.2MB GC pressure for batch=100, seq=512
3. MEDIUM: SessionOptions not disposed on success path in OnnxEmbeddingModel.Load
4. MEDIUM: Sync-over-async (.GetAwaiter().GetResult()) in constructor and ImageEmbeddings DI registration
5. MEDIUM: CLIP encoders (ClipImageEncoder, ClipTextEncoder) use default InferenceSession with no graph optimization or thread configuration
6. MEDIUM: LINQ chains in FindClosest/RankResults allocate iterators and intermediate collections — should use min-heap
7. MEDIUM: Tokenizer creates intermediate int[] via .ToArray() that could be iterated directly
8. MEDIUM: CLIP output extraction uses AsEnumerable<float>().ToArray() instead of direct tensor buffer access
9. LOW: Unnecessary .ToList() in GenerateAsync and TokenizeBatch when callers already pass lists

**Benchmark gaps identified:**
- No cold-start / model loading benchmark
- No mean pooling isolation benchmark
- No CLIP image/text encoder benchmarks
- No quantized vs. non-quantized comparison
- No VectorStore search at-scale benchmark

Full findings written to `.squad/decisions/inbox/parker-performance-audit-findings.md`.
