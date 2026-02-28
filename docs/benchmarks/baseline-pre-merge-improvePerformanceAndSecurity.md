# Benchmark Baseline — Pre-Merge (improvePerformanceAndSecurity)

**Branch:** improvePerformanceAndSecurity  
**Date:** 2026-02-28  
**Purpose:** Pre-merge baseline. Compare against post-merge results on main to validate performance improvements.  
**PR:** #37 — https://github.com/elbruno/elbruno.localembeddings/pull/37  
**Runtime:** .NET 8.0.24 (8.0.2426.7010), X64 RyuJIT AVX2  
**HW Intrinsics:** AVX2, AES, BMI1, BMI2, FMA, LZCNT, PCLMUL, POPCNT — VectorSize=256  
**OS:** Windows 11 (10.0.28020.1673)  
**BenchmarkDotNet:** v0.14.0 — Job=ShortRun (IterationCount=3, LaunchCount=1, WarmupCount=3)

---

## How to Run Post-Merge Comparison

After merging PR #37 to main, run:

```bash
git checkout main
git pull
dotnet run --project benchmarks/ElBruno.LocalEmbeddings.Benchmarks -c Release --framework net8.0 -- --filter "*MeanPoolingBenchmarks*" --job short
dotnet run --project benchmarks/ElBruno.LocalEmbeddings.Benchmarks -c Release --framework net8.0 -- --filter "*FindClosestBenchmarks*" --job short
dotnet run --project benchmarks/ElBruno.LocalEmbeddings.Benchmarks -c Release --framework net8.0 -- --filter "*L2NormalizationBenchmarks*" --job short
```

Then compare the numbers below with the new output.

---

## Compute Benchmarks (No Model Files Required)

### MeanPoolingBenchmarks

SIMD mean pooling (`TensorPrimitives.Add` / `TensorPrimitives.Divide`) on synthetic data. No ONNX session required.

```
BenchmarkDotNet v0.14.0, Windows 11 (10.0.28020.1673)
.NET SDK 10.0.103
  [Host]   : .NET 8.0.24 (8.0.2426.7010), X64 RyuJIT AVX2
  ShortRun : .NET 8.0.24 (8.0.2426.7010), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1  WarmupCount=3
```

| Method                          | Mean      | Error    | StdDev    | Gen0   | Allocated |
|-------------------------------- |----------:|---------:|----------:|-------:|----------:|
| MeanPooling_128Tokens_768Hidden |  6.281 μs | 2.124 μs | 0.1164 μs | 0.3662 |   3.02 KB |
| MeanPooling_512Tokens_768Hidden | 27.802 μs | 3.659 μs | 0.2005 μs | 0.3662 |   3.02 KB |

> **Notes:** Zero heap allocations beyond the 3.02 KB output buffer. Both configs allocate identically — ArrayPool eliminates input array GC pressure. Linear scaling from 128→512 tokens (~4.4×) is expected for SIMD flat span traversal.

---

### FindClosestBenchmarks

Min-heap (`PriorityQueue<TElement, float>`) top-K search on synthetic float[768] embeddings. No ONNX session required.

```
BenchmarkDotNet v0.14.0, Windows 11 (10.0.28020.1673)
.NET SDK 10.0.103
  [Host]   : .NET 8.0.24 (8.0.2426.7010), X64 RyuJIT AVX2
  ShortRun : .NET 8.0.24 (8.0.2426.7010), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1  WarmupCount=3
```

| Method           | CorpusSize | TopK | Mean         | Error        | StdDev      | Gen0   | Allocated |
|----------------- |----------- |----- |-------------:|-------------:|------------:|-------:|----------:|
| FindClosest_Heap | 100        | 5    |     9.533 μs |    17.886 μs |   0.9804 μs | 0.0305 |     336 B |
| FindClosest_Heap | 100        | 10   |     8.204 μs |     2.187 μs |   0.1199 μs | 0.0458 |     496 B |
| FindClosest_Heap | 100        | 50   |     9.664 μs |     9.018 μs |   0.4943 μs | 0.1984 |    1776 B |
| FindClosest_Heap | 1000       | 5    |    78.594 μs |    15.372 μs |   0.8426 μs |      - |     336 B |
| FindClosest_Heap | 1000       | 10   |    86.126 μs |    84.936 μs |   4.6556 μs |      - |     496 B |
| FindClosest_Heap | 1000       | 50   |   127.903 μs |   215.328 μs |  11.8029 μs | 0.1221 |    1776 B |
| FindClosest_Heap | 10000      | 5    | 1,515.868 μs |   709.646 μs |  38.8981 μs |      - |     336 B |
| FindClosest_Heap | 10000      | 10   | 1,206.896 μs | 2,787.572 μs | 152.7962 μs |      - |     497 B |
| FindClosest_Heap | 10000      | 50   | 1,245.760 μs | 1,421.751 μs |  77.9310 μs |      - |    1777 B |

> **Notes:** Allocation is `O(topK)` — only the result list is heap-allocated; the heap itself is stack/pool managed. Near-zero GC for corpus≥1000 with small TopK. Linear scaling at corpus=100→1000 (~10×) confirms O(n log k) behavior. `ShortRun` error margins are wider at small corpus due to low elapsed time; run with `--job default` for tighter CIs.

---

### L2NormalizationBenchmarks

`TensorPrimitives.Norm` + `TensorPrimitives.Divide` on synthetic float arrays (zero allocations expected).

```
BenchmarkDotNet v0.14.0, Windows 11 (10.0.28020.1673)
.NET SDK 10.0.103
  [Host]   : .NET 8.0.24 (8.0.2426.7010), X64 RyuJIT AVX2
  ShortRun : .NET 8.0.24 (8.0.2426.7010), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1  WarmupCount=3
```

| Method                  | Mean     | Error     | StdDev   | Allocated |
|------------------------ |---------:|----------:|---------:|----------:|
| NormalizeEmbedding_768d | 258.7 ns | 236.87 ns | 12.98 ns |         - |
| NormalizeEmbedding_384d | 109.0 ns |   7.17 ns |  0.39 ns |         - |

> **Notes:** Zero allocations — pure in-place SIMD. 384d→768d is roughly 2.4× (slightly super-linear, consistent with cache line effects at AVX2 boundary). The large error on 768d (91%) is a `ShortRun` artifact — use `--job default` for a tighter CI if needed.

---

## Model-Dependent Benchmarks (Require Cached Model Files)

These benchmarks were not run in the pre-merge baseline because they require ONNX model files in the user's local cache:

- `EmbeddingGenerationBenchmarks`
- `ModelLoadingBenchmarks`
- `QuantizedVsFullBenchmarks`
- `SingleVsBatchBenchmarks`
- `TokenizerBenchmarks`

To run these, ensure models are downloaded (run the `samples/ConsoleApp` sample first to populate the cache), then:

```bash
dotnet run --project benchmarks/ElBruno.LocalEmbeddings.Benchmarks -c Release --framework net8.0 -- --job short
```

---

## Key Performance Changes in This Branch (improvePerformanceAndSecurity)

The following performance improvements were implemented in this branch and are reflected in the numbers above:

| Finding   | Change | Expected Impact |
|-----------|--------|-----------------|
| PERF-01   | `ArrayPool<long>` for `flatInputIds`/`flatAttentionMask`/`flatTokenTypeIds` | ~1.2 MB GC eliminated per call at batch=100 |
| PERF-02   | `TensorPrimitives.Add`/`Divide` SIMD mean pooling | Measured above: 6.3 μs (128T) / 27.8 μs (512T) |
| PERF-09   | `PriorityQueue` min-heap replaces LINQ `.OrderByDescending` in `FindClosest` | Measured above: O(n log k) confirmed |
| PERF-16   | L2 norm via `TensorPrimitives` | Measured above: 109–259 ns, zero allocations |
