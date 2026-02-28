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

## Core Context

> Summarized by Scribe on 2026-02-28. Full per-phase details are in `.squad/orchestration-log/` and `.squad/log/`.

### Performance Audit (2025-07-16) — 17 Findings, All Phases Complete

Full codebase audit covering all 5 source projects. 17 findings (2 HIGH, 12 MEDIUM, 3 LOW). All actionable phases (1–4) complete; Phase 5 (benchmarks) in progress.

**Resolved (by phase):**
- **Phase 1 — PERF-01/02 (HIGH):** `ArrayPool<long>.Shared.Rent/Return` for flattening arrays (~1.2 MB GC/call at batch=100). `TensorPrimitives.Add`/`Divide` replace scalar mean pooling loop in `OnnxEmbeddingModel.ApplyMeanPooling` (uses `DenseTensor<float>.Buffer.Span`, flat offset `(batch * seq + seq) * hidden`).
- **Phase 2 — PERF-03/15/16:** `using var sessionOptions` in `OnnxEmbeddingModel.Load` (success-path leak fixed). Both CLIP encoders now use `ORT_ENABLE_ALL`, `ORT_SEQUENTIAL`, `InterOpNumThreads=1`, `IntraOpNumThreads=ProcessorCount`.
- **Phase 3 — PERF-09/10/06/07/08/12/13:** `PriorityQueue` min-heap for `FindClosest` (both overloads) and `ImageSearchEngine.RankResults` — O(n log n) → O(n log k). CLIP output extraction via `AsTensor<float>().ToArray()`. `IReadOnlyList<int>` iterated directly in `Tokenizer.Tokenize` (eliminates `int[]`). `as IList<T> ?? .ToList()` pattern applied to `TokenizeBatch` and `GenerateAsync`.
- **Phase 4 — PERF-04/05:** Async documentation expanded in both `ServiceCollectionExtensions` files and `LocalEmbeddingGenerator.CreateAsync`. Residual: `GenerateAsync` `values.ToList()` → `values as IList<string> ?? values.ToList()`.

**Key technical details:**
- `PriorityQueue.TryPeek(out _, out float lowestScore)` inspects minimum priority (`Peek()` returns element, not priority)
- `DequeueEnqueue` is .NET 8+ only — safe for this project's targets (net8.0 / net10.0)
- Second `FindClosest` overload tiebreaker: O(k log k) post-heap sort of topK items only
- Sync-over-async in constructor and DI factory preserved for backward compatibility; `CreateAsync()` is the documented recommended alternative for ASP.NET Core contexts

**Open (Phase 5):** Benchmark expansion — 7 missing benchmark classes in `samples/BenchmarkSample/` (cold start, mean pooling, normalization, CLIP encoders, VectorStore search, single vs. batch, quantized comparison).

## Learnings

### 2025-07-16: Phase 5 Benchmark Infrastructure Expansion Implemented

**New project: `benchmarks/ElBruno.LocalEmbeddings.Benchmarks/`**

Created a dedicated benchmark project targeting `net8.0;net10.0` (matching library targets), separate from the existing `samples/BenchmarkSample` which only targets `net10.0`. Registered in the `/benchmarks/` solution folder in `ElBruno.LocalEmbeddings.slnx`.

**Decision: New project in `benchmarks/` rather than extending `samples/BenchmarkSample`**
- Maintains clean separation between samples (demo code) and engineered performance benchmarks
- Enables dual-framework targeting (`net8.0;net10.0`) without changing an existing sample
- Follows repo structure convention: `src/`, `tests/`, `samples/`, `benchmarks/`

**8 new benchmark classes:**
1. `ModelLoadingBenchmarks` — cold vs warm load timing; skips gracefully when model not cached
2. `MeanPoolingBenchmarks` — SIMD mean pooling on synthetic data; no ONNX session required
3. `EmbeddingGenerationBenchmarks` — end-to-end single + batch-10 + batch-100 throughput
4. `TokenizerBenchmarks` — short/long text tokenization; returns `long[]` (library's actual output)
5. `FindClosestBenchmarks` — min-heap `FindClosest` with `[Params]` on CorpusSize and TopK; fully synthetic
6. `L2NormalizationBenchmarks` — `TensorPrimitives.Norm` + `TensorPrimitives.Divide`; no model required
7. `SingleVsBatchBenchmarks` — 10 individual calls vs 1 batch call with 10 items
8. `QuantizedVsFullBenchmarks` — FP32 vs INT8 throughput comparison

**Shared helper:** `BenchmarkHelpers.TryResolveModelDirectory()` avoids duplicating the cache-path resolution across 5 model-dependent benchmark classes.

**CI safety pattern:** All model-dependent benchmarks use `LocalEmbeddingGenerator?` (nullable), set in `[GlobalSetup]` with try/catch, and guard each `[Benchmark]` method with `if (_generator is null) return;`. Benchmarks compile and "run" cleanly in CI; they simply no-op when no model is cached.

**Build result:** `dotnet build` succeeded with 0 warnings, 0 errors across net8.0 + net10.0 for all projects.

### 2026-02-28: Post-Merge Benchmark Comparison Completed (main, PR #37)

**Comparison file:** `docs/performance/post-merge-comparison.md`  
**Baseline (also copied):** `docs/performance/baseline-pre-merge-improvePerformanceAndSecurity.md`  
**Branch:** main (after merging `improvePerformanceAndSecurity`, PR #37)  
**Commit:** `0698292`

**Benchmarks run:** Same 3 compute-only suites (`--job short`, `--framework net8.0`):
- `MeanPoolingBenchmarks`, `FindClosestBenchmarks`, `L2NormalizationBenchmarks`

**Key findings:**
- **L2Norm:** −25% (768d: 258.7 → 194.0 ns) and −13% (384d: 109.0 → 94.8 ns) — clearest post-merge improvement; zero allocations maintained
- **FindClosest corpus=10000:** −34% at TopK=5 (1516 → 999 μs), ~0% at TopK=10, −8% at TopK=50
- **FindClosest corpus=1000/K50:** −37% (128 → 80 μs) — wide pre-merge error margin, but directionally consistent
- **MeanPooling 128T:** −3.8% (within noise); MeanPooling 512T: +14.8% apparent but 50.7% CI margin — not a real regression
- **No allocations added** anywhere; memory profiles identical to baseline

**Note:** MeanPooling 512T "regression" is ShortRun measurement noise (50.7% CI). Run `--job default` to confirm.

### 2026-02-28: Cross-Agent Update — Lambert Test Coverage Summary

**From:** Scribe (cross-agent propagation)

Lambert provided comprehensive test coverage for all performance work across Phases 1–4:

- **Phase 1 (PERF-01/02):** `MeanPoolingTests.cs` — 8 tests. Validates SIMD correctness of `ApplyMeanPooling` and ArrayPool regression. Required making `ApplyMeanPooling` `internal` and adding `InternalsVisibleTo` to the csproj.
- **Phase 2 (PERF-03/15/16):** No dedicated tests — validated by full build + existing suite (0 failures).
- **Phase 3 (PERF-09/08/12/13):** `FindClosestTests.cs` (12 tests) — 9 unit tests with parity checks against LINQ reference (deterministic Random(42) seed), 3 integration tokenizer regression tests.
- **Phase 4 (PERF-04/05):** `AsyncPatternTests.cs` — 2 tests (reflection check for `CreateAsync` overloads, DI registration via `IServiceCollection` inspection).
- **Total Lambert tests for performance work: 22 tests** (8 + 0 + 12 + 2)

Key cross-agent note: Lambert's Phase 3 parity tests use the public extension method API (`using ElBruno.LocalEmbeddings.Extensions`). Signature is `FindClosest(Embedding<float> query, IReadOnlyList<Embedding<float>> corpus, int topK, float? minScore)`. Any future signature change will break these tests.

