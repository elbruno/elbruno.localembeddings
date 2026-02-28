# Performance Audit — ElBruno.LocalEmbeddings

**Author:** Parker (Performance Engineer)  
**Date:** 2025-07-16  
**Scope:** Full codebase audit covering memory allocations, ONNX session configuration, async patterns, SIMD usage, batch processing, model loading, tokenization, search, and benchmarking.

---

## Executive Summary

The codebase is architecturally sound with good fundamentals — ONNX sessions are reused, TensorPrimitives is used for SIMD, and batched inference is supported. However, there are **17 findings** across all severity levels. The most impactful issues are: unnecessary heap allocations in the hot embedding path (3 array allocations per inference that could use ArrayPool), mean pooling using scalar element-by-element access instead of SIMD, a sync-over-async anti-pattern in the constructor, LINQ allocations in hot search paths, and missing `SessionOptions` disposal. A benchmark project exists but needs expansion.

---

## Findings

### FINDING-01: Unnecessary Array Allocations in GenerateEmbeddings (Flattening)

- **Impact:** HIGH
- **Location:** `src/ElBruno.LocalEmbeddings/OnnxEmbeddingModel.cs:320-323`
- **Description:** Three `new long[]` arrays are allocated every call to `GenerateEmbeddings`: `flatInputIds`, `flatAttentionMask`, and `flatTokenTypeIds`. For batch size N and sequence length S, this is 3 × N × S × 8 bytes per call. With N=100, S=512, that's ~1.2MB per inference call, all immediately becoming garbage.
- **Root cause:** Arrays are heap-allocated and never pooled or reused.
- **Recommended fix:** Use `ArrayPool<long>.Shared.Rent()` for `flatInputIds`, `flatAttentionMask`, and `flatTokenTypeIds`. Return them in a `finally` block. The `flatTokenTypeIds` array (all zeros) could be a cached static array or use `Array.Clear` on a rented buffer.
- **Benchmark required:** `GenerateEmbeddings_BatchSize` benchmark comparing Gen0/Gen1 collections and allocation bytes before/after pooling. Use `[MemoryDiagnoser]`.
- **Test required:** Existing embedding generation tests validate correctness. Add a test that generates embeddings with batch sizes 1, 10, 100 and verifies identical results after the pooling change.

---

### FINDING-02: Mean Pooling Uses Scalar Element-by-Element Access Instead of SIMD

- **Impact:** HIGH
- **Location:** `src/ElBruno.LocalEmbeddings/OnnxEmbeddingModel.cs:390-428`
- **Description:** `ApplyMeanPooling` iterates over every element in the hidden dimension with a scalar loop (`embedding[hidden] += outputTensor[batch, seq, hidden] * mask`). This is the single hottest loop in inference post-processing and is not vectorized. The `Tensor<float>` indexer also performs bounds checking per access.
- **Root cause:** Accessing `outputTensor[batch, seq, hidden]` element-by-element through the indexer prevents SIMD and triggers bounds checks. The division loop (`embedding[hidden] /= tokenCount`) is also scalar.
- **Recommended fix:** Extract the output tensor buffer as a `ReadOnlySpan<float>` (via `outputTensor.Buffer.Span` or equivalent), slice to the correct `[batch, seq]` offset, and use `TensorPrimitives.MultiplyAdd` or `TensorPrimitives.Add` for the accumulation. Use `TensorPrimitives.Divide` for the final normalization step. This converts the inner loop from O(hidden) scalar ops to SIMD-width ops.
- **Benchmark required:** `ApplyMeanPooling_Scalar_vs_SIMD` micro-benchmark with representative tensor shapes (batch=10, seq=512, hidden=384).
- **Test required:** Unit test comparing mean-pooled output of the optimized path against the current scalar implementation with known input tensors.

---

### FINDING-03: SessionOptions Not Disposed on Success Path

- **Impact:** MEDIUM
- **Location:** `src/ElBruno.LocalEmbeddings/OnnxEmbeddingModel.cs:79-107`
- **Description:** `SessionOptions` is created at line 82 but never disposed after being passed to `InferenceSession`. While ORT may take ownership internally, the .NET `SessionOptions` object implements `IDisposable` and should be disposed after the session is created, or wrapped in `using`. On the error path (line 101-103) it is disposed, but on success it is leaked.
- **Root cause:** Missing `using` statement or explicit `Dispose()` on the success path.
- **Recommended fix:** Wrap `sessionOptions` in a `using` declaration or call `sessionOptions.Dispose()` after successful `InferenceSession` creation. Verify ORT does not re-access sessionOptions after session creation (it doesn't — options are copied during construction).
- **Benchmark required:** Not performance-critical, but reduces managed memory pressure. Measurable only in long-running scenarios with repeated Load/Dispose cycles.
- **Test required:** Existing Load tests should pass unchanged.

---

### FINDING-04: Sync-over-Async in Constructor (GetAwaiter().GetResult())

- **Impact:** MEDIUM
- **Location:** `src/ElBruno.LocalEmbeddings/LocalEmbeddingGenerator.cs:271`
- **Description:** `ResolveModelDirectory` calls `.GetAwaiter().GetResult()` on an async download method. This blocks the calling thread and can deadlock in SynchronizationContext-aware environments (ASP.NET, UI frameworks).
- **Root cause:** Constructor cannot be async, so async model download is forced synchronous.
- **Recommended fix:** The codebase already has `CreateAsync` factory methods (lines 110-165) which are the correct pattern. Consider marking the constructor `internal` or adding an `[Obsolete]` warning pointing to `CreateAsync`. For the DI path (`ServiceCollectionExtensions.AddLocalEmbeddingsCore` line 196-199), consider using async factory registration to avoid sync-over-async during DI resolution.
- **Benchmark required:** Not a throughput issue but a startup latency and deadlock risk issue. Measure cold-start time with `CreateAsync` vs constructor.
- **Test required:** Verify `CreateAsync` produces identical functionality to constructor path.

---

### FINDING-05: Sync-over-Async in ImageEmbeddings DI Registration

- **Impact:** MEDIUM
- **Location:** `src/ElBruno.LocalEmbeddings.ImageEmbeddings/Extensions/ServiceCollectionExtensions.cs:89`
- **Description:** `EnsureModels` calls `downloader.EnsureModelDownloadedAsync(...).GetAwaiter().GetResult()` inside a singleton factory, blocking the DI resolution thread.
- **Root cause:** DI singleton factories cannot be async, and no async initialization pattern is used.
- **Recommended fix:** Consider a lazy initialization pattern or an `IHostedService` that downloads models asynchronously during startup. Alternatively, document that `EnsureModelDownloaded = true` performs blocking I/O during DI resolution.
- **Benchmark required:** Measure startup time with and without model pre-caching.
- **Test required:** Existing DI integration tests should cover this.

---

### FINDING-06: LINQ .ToArray() Allocation in ONNX Output Extraction

- **Impact:** MEDIUM
- **Location:** `src/ElBruno.LocalEmbeddings.ImageEmbeddings/ClipImageEncoder.cs:90`
- **Description:** `results.First().AsEnumerable<float>().ToArray()` creates an intermediate `IEnumerable<float>` and then copies to a new `float[]`. The ONNX output tensor already contains the data in a contiguous buffer.
- **Root cause:** Using LINQ enumeration instead of direct tensor buffer access.
- **Recommended fix:** Use `results.First().AsTensor<float>()` and then copy from the tensor's buffer directly: `var tensor = results.First().AsTensor<float>(); var output = tensor.ToArray();` or better, `tensor.Buffer.Span.ToArray()`. This avoids the boxing/unboxing overhead of `AsEnumerable<float>()`.
- **Benchmark required:** `ClipImageEncoder_OutputExtraction` benchmark measuring allocation difference.
- **Test required:** Existing image encoding tests verify correctness.

---

### FINDING-07: Same LINQ .ToArray() Allocation in ClipTextEncoder

- **Impact:** MEDIUM
- **Location:** `src/ElBruno.LocalEmbeddings.ImageEmbeddings/ClipTextEncoder.cs:82`
- **Description:** Same pattern as FINDING-06: `results.First().AsEnumerable<float>().ToArray()`.
- **Root cause:** Same as FINDING-06.
- **Recommended fix:** Same as FINDING-06.
- **Benchmark required:** Same as FINDING-06 but for text encoder.
- **Test required:** Existing text encoding tests verify correctness.

---

### FINDING-08: Tokenizer Allocates Intermediate int[] via .ToArray()

- **Impact:** MEDIUM
- **Location:** `src/ElBruno.LocalEmbeddings/Tokenizer.cs:108`
- **Description:** `encoding.ToArray()` creates a `int[]` from the `IReadOnlyList<int>` returned by `EncodeToIds`. This is an intermediate allocation that is only iterated once to copy into `long[]` arrays.
- **Root cause:** Type mismatch between tokenizer output (`int`) and ONNX input (`long`) forces a copy with an intermediate allocation.
- **Recommended fix:** Iterate the `IReadOnlyList<int>` directly instead of calling `.ToArray()`. Replace lines 108-120 with a single loop that reads from `encoding[i]` and writes to `inputIds[i]` and `attentionMask[i]`. This eliminates one `int[]` allocation per tokenize call.
- **Benchmark required:** `Tokenizer_SingleText` benchmark with `[MemoryDiagnoser]` to measure allocation reduction.
- **Test required:** Existing tokenizer tests verify correctness.

---

### FINDING-09: LINQ Allocations in FindClosest / Similarity Search Hot Paths

- **Impact:** MEDIUM
- **Location:** `src/ElBruno.LocalEmbeddings/Extensions/EmbeddingExtensions.cs:188-201` and `EmbeddingExtensions.cs:80-81`
- **Description:** `FindClosest` uses `.Select()`, `.Where()`, `.OrderByDescending()`, `.ThenBy()`, `.Take()`, `.ToList()` — a chain of LINQ allocations including delegate allocations and iterator objects. In `Similarity()`, `.ToList()` materializes both collections. For large corpora (1000+ items), this creates significant GC pressure.
- **Root cause:** Idiomatic LINQ is convenient but allocates iterators, delegates, and intermediate collections.
- **Recommended fix:** For `FindClosest`, replace LINQ with a manual partial sort (min-heap of size topK) that iterates the corpus once with O(n log k) complexity and zero intermediate allocations. For `Similarity()`, accept `IReadOnlyList` instead of `IEnumerable` to avoid the `.ToList()` materialization.
- **Benchmark required:** `FindClosest_LINQ_vs_Heap` benchmark with corpus sizes 100, 1000, 10000.
- **Test required:** Existing `FindClosest` tests verify correctness. Add edge cases for topK > corpus size.

---

### FINDING-10: ImageSearchEngine.RankResults Allocates List + LINQ Per Query

- **Impact:** MEDIUM
- **Location:** `src/ElBruno.LocalEmbeddings.ImageEmbeddings/ImageSearchEngine.cs:115-130`
- **Description:** `RankResults` creates a new `List<(string, float)>`, adds all results, then sorts with `.OrderByDescending().Take().ToList()`. For an index of N images, this allocates O(N) tuples plus sort overhead per query.
- **Root cause:** Full materialization and sort instead of partial sort.
- **Recommended fix:** Use a fixed-size min-heap (or `PriorityQueue<string, float>`) of size `topK`. Iterate the index once, maintaining only the top-K results. This reduces allocations from O(N) to O(K) and avoids the full sort.
- **Benchmark required:** `ImageSearch_RankResults` benchmark with index sizes 100, 1000, 10000.
- **Test required:** Existing search tests verify correctness.

---

### FINDING-11: ClipTokenizer String Allocations in Hot Path

- **Impact:** MEDIUM
- **Location:** `src/ElBruno.LocalEmbeddings.ImageEmbeddings/ClipTokenizer.cs:53-74`
- **Description:** `text.ToLowerInvariant()` allocates a new string. `.Split()` allocates a string array. `c.ToString()` (line 66) allocates a new string per character in the fallback path. These happen on every `Encode` call.
- **Root cause:** String operations that allocate per call instead of using span-based APIs.
- **Recommended fix:** Use `text.AsSpan()` with `char.ToLowerInvariant()` for case conversion. For the character fallback, use `stackalloc char[1]` or `MemoryMarshal.CreateReadOnlySpan` to avoid `ToString()` allocation. Consider pre-computing lowercase vocab keys for O(1) lookup. The `.Split()` could use `string.AsSpan()` with `MemoryExtensions.Split` on newer TFMs.
- **Benchmark required:** `ClipTokenizer_Encode` benchmark with `[MemoryDiagnoser]`.
- **Test required:** Existing tokenizer tests verify correctness.

---

### FINDING-12: Tokenizer.TokenizeBatch Materializes IEnumerable to List

- **Impact:** LOW
- **Location:** `src/ElBruno.LocalEmbeddings/Tokenizer.cs:161`
- **Description:** `texts.ToList()` materializes the input. When called from `GenerateAsync` which already passes a `List<string>` (line 180 of LocalEmbeddingGenerator.cs), this creates an unnecessary copy.
- **Root cause:** Method signature accepts `IEnumerable<string>` but immediately materializes.
- **Recommended fix:** Add an overload accepting `IReadOnlyList<string>` that skips materialization. Or check `texts is IReadOnlyList<string> list` and use it directly.
- **Benchmark required:** `TokenizeBatch_ListVsEnumerable` — likely small impact but measurable at scale.
- **Test required:** Existing batch tokenization tests.

---

### FINDING-13: GenerateAsync Materializes IEnumerable Values to List

- **Impact:** LOW
- **Location:** `src/ElBruno.LocalEmbeddings/LocalEmbeddingGenerator.cs:180`
- **Description:** `values.ToList()` materializes the input enumerable. When callers already pass a list or array, this is a wasted allocation.
- **Root cause:** Same pattern as FINDING-12.
- **Recommended fix:** Use `values as IReadOnlyList<string> ?? values.ToList()` to avoid re-materialization.
- **Benchmark required:** Marginal — only matters for high-frequency single-item calls.
- **Test required:** Existing tests.

---

### FINDING-14: DenseTensor Shape Array Allocation

- **Impact:** LOW
- **Location:** `src/ElBruno.LocalEmbeddings/OnnxEmbeddingModel.cs:332`
- **Description:** `new long[] { batchSize, sequenceLength }` allocates a small array for the tensor shape on every call. This is minor but could use `stackalloc` or a cached shape pattern.
- **Root cause:** ORT API requires an array for shape.
- **Recommended fix:** If ORT accepts `ReadOnlySpan<int>` for shape (newer versions), use `stackalloc`. Otherwise, this is a minor allocation (~24 bytes) and can be deferred.
- **Benchmark required:** Not worth benchmarking alone — include in overall allocation reduction.
- **Test required:** N/A.

---

### FINDING-15: ClipImageEncoder Missing Session Options

- **Impact:** MEDIUM
- **Location:** `src/ElBruno.LocalEmbeddings.ImageEmbeddings/ClipImageEncoder.cs:34`
- **Description:** `new InferenceSession(modelPath)` uses default session options — no graph optimization level, no thread configuration, no execution mode specified. The main library's `OnnxEmbeddingModel` correctly sets `GraphOptimizationLevel.ORT_ENABLE_ALL` and configures thread counts.
- **Root cause:** Image embeddings encoders were added without mirroring the session configuration pattern.
- **Recommended fix:** Apply the same session options pattern: `GraphOptimizationLevel.ORT_ENABLE_ALL`, configurable `InterOpNumThreads`/`IntraOpNumThreads`, and `ExecutionMode`. Add these as options to `ImageEmbeddingsOptions`.
- **Benchmark required:** `ClipImageEncoder_WithOptimizedSessionOptions` comparing default vs. optimized options.
- **Test required:** Existing image encoder tests should pass with optimized session options.

---

### FINDING-16: ClipTextEncoder Missing Session Options

- **Impact:** MEDIUM
- **Location:** `src/ElBruno.LocalEmbeddings.ImageEmbeddings/ClipTextEncoder.cs:30`
- **Description:** Same as FINDING-15 — `new InferenceSession(modelPath)` with default options.
- **Root cause:** Same as FINDING-15.
- **Recommended fix:** Same as FINDING-15.
- **Benchmark required:** Same as FINDING-15 but for text encoder.
- **Test required:** Same as FINDING-15.

---

### FINDING-17: Benchmark Project Gaps

- **Impact:** MEDIUM (process risk)
- **Location:** `samples/BenchmarkSample/`
- **Description:** The benchmark project exists with 3 benchmark classes (embedding, tokenizer, similarity), which is a good start. However, it is missing benchmarks for:
  1. **Cold start / model loading** — no benchmark for `new LocalEmbeddingGenerator()` or `CreateAsync()` time.
  2. **Mean pooling** — no isolated benchmark for the post-inference pooling step.
  3. **Normalization** — no isolated L2 normalization benchmark.
  4. **Image encoder** — no benchmarks for CLIP image/text encoding.
  5. **VectorStore search** — no benchmark for `InMemoryVectorStoreCollection.SearchAsync` at scale.
  6. **Single vs. batch comparison** — the batch benchmark exists but doesn't directly compare per-item throughput.
  7. **Quantized vs. non-quantized model** — no benchmark comparing inference speed with quantized models.
- **Root cause:** Benchmarks were added as a sample, not as a comprehensive performance regression suite.
- **Recommended fix:** Expand the benchmark project with the missing benchmarks listed above. Consider moving it from `samples/` to a dedicated `benchmarks/` directory to signal its purpose. Add a CI-friendly way to run a subset of benchmarks.
- **Benchmark required:** This IS the benchmark gap.
- **Test required:** N/A (benchmarks are the tests).

---

## Summary Table

| ID | Severity | Category | File | Short Description |
|----|----------|----------|------|-------------------|
| 01 | HIGH | Memory | OnnxEmbeddingModel.cs | Array allocations in flattening — use ArrayPool |
| 02 | HIGH | SIMD | OnnxEmbeddingModel.cs | Mean pooling scalar loop — use TensorPrimitives |
| 03 | MEDIUM | Resource | OnnxEmbeddingModel.cs | SessionOptions not disposed on success |
| 04 | MEDIUM | Async | LocalEmbeddingGenerator.cs | Sync-over-async in constructor |
| 05 | MEDIUM | Async | ImageEmbeddings ServiceCollectionExtensions.cs | Sync-over-async in DI factory |
| 06 | MEDIUM | Memory | ClipImageEncoder.cs | LINQ .ToArray() on ONNX output |
| 07 | MEDIUM | Memory | ClipTextEncoder.cs | LINQ .ToArray() on ONNX output |
| 08 | MEDIUM | Memory | Tokenizer.cs | Intermediate int[] allocation |
| 09 | MEDIUM | Memory | EmbeddingExtensions.cs | LINQ chain in FindClosest |
| 10 | MEDIUM | Memory | ImageSearchEngine.cs | Full-materialization sort in RankResults |
| 11 | MEDIUM | Memory | ClipTokenizer.cs | String allocations per encode |
| 12 | LOW | Memory | Tokenizer.cs | Unnecessary .ToList() in batch |
| 13 | LOW | Memory | LocalEmbeddingGenerator.cs | Unnecessary .ToList() in GenerateAsync |
| 14 | LOW | Memory | OnnxEmbeddingModel.cs | Shape array allocation |
| 15 | MEDIUM | Config | ClipImageEncoder.cs | Missing ONNX session options |
| 16 | MEDIUM | Config | ClipTextEncoder.cs | Missing ONNX session options |
| 17 | MEDIUM | Process | BenchmarkSample/ | Missing benchmark coverage |

---

## What's Already Good

- ✅ **ONNX session reuse** — Sessions are created once and reused. Thread-safe for concurrent inference.
- ✅ **TensorPrimitives for cosine similarity and L2 normalization** — SIMD-accelerated in `EmbeddingExtensions.cs`, `OnnxEmbeddingModel.cs:380-385`, `ClipImageEncoder.cs:98-105`, `ClipTextEncoder.cs:89-96`.
- ✅ **Batched inference** — `GenerateEmbeddings` takes arrays of inputs and runs them through ORT in a single call.
- ✅ **Configurable thread counts** — `InterOpNumThreads` and `IntraOpNumThreads` are exposed as options.
- ✅ **Graph optimization level** — Set to `ORT_ENABLE_ALL` (optimal).
- ✅ **Model caching** — Downloaded models are cached locally, avoiding repeated downloads.
- ✅ **ConfigureAwait(false)** — Used consistently across all async methods in the library.
- ✅ **Async factory methods** — `CreateAsync` exists as the correct alternative to the sync constructor.
- ✅ **Benchmark project exists** — BenchmarkDotNet with `[MemoryDiagnoser]` on 3 benchmark classes.

---

## Recommended Priority Order for Fixes

1. **FINDING-02** (Mean pooling SIMD) — Highest throughput impact, directly on the inference hot path.
2. **FINDING-01** (ArrayPool for flattening) — Reduces GC pressure proportional to batch size.
3. **FINDING-09** (FindClosest heap-based search) — Affects all search consumers at scale.
4. **FINDING-08** (Tokenizer intermediate allocation) — Called per-text, high frequency.
5. **FINDING-15/16** (CLIP session options) — Easy win, measurable for image embedding users.
6. **FINDING-06/07** (CLIP output extraction) — Straightforward fix, reduces allocations.
7. **FINDING-03** (SessionOptions disposal) — Resource hygiene.
8. **FINDING-17** (Benchmark expansion) — Required to measure all the above improvements.
9. **FINDING-10/11** (ImageSearch/ClipTokenizer) — Good for completeness.
10. **FINDING-04/05** (Sync-over-async) — Architectural, lower priority unless deadlocks reported.
11. **FINDING-12/13/14** (Minor allocations) — Polish pass.

---

*Report generated by Parker, Performance Engineer. All findings are based on static code analysis. Benchmark numbers should be collected before and after each fix to validate impact.*
