# Parker: Phase 3 Memory & Search Optimizations Implemented

**By:** Parker (Performance Engineer)  
**Date:** 2025-07-16  
**Status:** Implemented — build clean, all tests green

---

## Summary

Phase 3 implements O(n log k) partial sorts via `PriorityQueue` min-heaps, eliminates intermediate allocations in the tokenizer and CLIP encoders, and removes redundant `.ToList()` calls in the hot inference path.

---

## Changes Made

### PERF-09 — `EmbeddingExtensions.FindClosest` (both overloads)
**File:** `src/ElBruno.LocalEmbeddings/Extensions/EmbeddingExtensions.cs`

Replaced LINQ `OrderByDescending().Take().ToList()` (O(n log n)) with a `PriorityQueue<TElement, float>` min-heap of capacity `topK` (O(n log k)). Key implementation note: use `TryPeek(out _, out float lowestScore)` to inspect the minimum priority value — `Peek()` returns the element, not the priority. The second overload (with `ThenBy(index)` tiebreaker) preserves the secondary sort by sorting the final topK-item result list after heap extraction.

### PERF-10 — `ImageSearchEngine.RankResults`
**File:** `src/ElBruno.LocalEmbeddings.ImageEmbeddings/ImageSearchEngine.cs`

Same min-heap approach as PERF-09. Eliminates the intermediate `List<(string, float)>` that scaled with corpus size (all n items allocated, most discarded after take).

### PERF-06/07 — CLIP encoder output extraction
**Files:** `src/ElBruno.LocalEmbeddings.ImageEmbeddings/ClipImageEncoder.cs`, `ClipTextEncoder.cs`

Replaced `results.First().AsEnumerable<float>().ToArray()` with `results.First().AsTensor<float>().ToArray()`. The `AsTensor<T>()` path returns the underlying `DenseTensor<T>` and calls its optimized `ToArray()`, bypassing LINQ IEnumerable iterator allocation and overhead.

### PERF-08 — Tokenizer intermediate int[] elimination
**File:** `src/ElBruno.LocalEmbeddings/Tokenizer.cs`

Removed the `tokenIds = encoding.ToArray()` step. `EncodeToIds()` returns `IReadOnlyList<int>` which is already indexable — the for loop that copies to `long[] inputIds` now reads directly from `encoding[i]`, eliminating one `int[]` allocation per `Tokenize` call. Saves ~100 allocations per batch=100 inference call.

### PERF-12/13 — Redundant .ToList() removal
**Files:** `src/ElBruno.LocalEmbeddings/LocalEmbeddingGenerator.cs`, `src/ElBruno.LocalEmbeddings/Tokenizer.cs`

- `GenerateAsync`: removed `.ToList()` on `rawEmbeddings.Select(...)` passed to `GeneratedEmbeddings<T>` constructor (accepts `IEnumerable<T>`).
- `TokenizeBatch`: changed `texts.ToList()` to `texts as IList<string> ?? texts.ToList()`, so when `GenerateAsync` passes its `List<string>` the inner re-allocation is skipped.

---

## Correctness

- All 396 tests passed on both net8.0 and net10.0 (10 skipped — require real CLIP model files)
- `dotnet build` — 0 errors, 0 warnings (`TreatWarningsAsErrors=true` respected)
- Numerical results are identical: heap extraction + reverse produces same descending-order top-K as the LINQ sort

---

## Team Impact

The `PriorityQueue` pattern is now the standard for top-K searches in this codebase. Any future search methods (e.g., VectorData store search) should follow the same `O(n log k)` heap pattern rather than sort-then-take.
