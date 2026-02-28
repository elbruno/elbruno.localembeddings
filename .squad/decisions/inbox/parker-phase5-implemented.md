# Decision: Phase 5 Benchmark Infrastructure Expansion Implemented

**By:** Parker (Performance Engineer)  
**Date:** 2025-07-16  
**Status:** Implemented

## Summary

Created a new benchmark project `benchmarks/ElBruno.LocalEmbeddings.Benchmarks/` with 8 benchmark classes covering the gaps identified in the Phase 1 performance audit. Added to the solution file under a `/benchmarks/` folder.

## What Was Built

### Project location

`benchmarks/ElBruno.LocalEmbeddings.Benchmarks/ElBruno.LocalEmbeddings.Benchmarks.csproj`

- Targets `net8.0;net10.0` (matches the library)
- References `BenchmarkDotNet 0.14.0` and the `ElBruno.LocalEmbeddings` project
- Inherits `TreatWarningsAsErrors=true` and `Nullable=enable` from `Directory.Build.props`

### Decision: New project vs extending BenchmarkSample

Extended in a new project (`benchmarks/`) rather than modifying `samples/BenchmarkSample` because:
1. `BenchmarkSample` is a demo/sample targeting only `net10.0`; the new project is an engineered performance harness that needs dual-framework coverage
2. Keeps samples clean and end-user focused

### 8 benchmark classes

| Class | Model required | Key technique |
|---|---|---|
| `ModelLoadingBenchmarks` | Yes (skips if absent) | Measures `LocalEmbeddingGenerator` init time |
| `MeanPoolingBenchmarks` | No | TensorPrimitives SIMD on synthetic float[] |
| `EmbeddingGenerationBenchmarks` | Yes (skips if absent) | Single + batch-10 + batch-100 |
| `TokenizerBenchmarks` | Yes (skips if absent) | Short vs long text alloc profiling |
| `FindClosestBenchmarks` | No | `[Params]` CorpusSize × TopK; min-heap |
| `L2NormalizationBenchmarks` | No | TensorPrimitives.Norm + Divide |
| `SingleVsBatchBenchmarks` | Yes (skips if absent) | 10×single vs 1×batch(10) |
| `QuantizedVsFullBenchmarks` | Yes (skips if absent) | FP32 vs INT8 throughput |

### CI safety

All model-dependent benchmarks use nullable generator fields, `try/catch` in `[GlobalSetup]`, and an early `return` guard in each `[Benchmark]` method. They compile and run cleanly in CI (producing zero-duration no-op results when no model cache is available).

### Shared helper

`BenchmarkHelpers.TryResolveModelDirectory()` resolves the default HuggingFace model cache path (Windows: `%LOCALAPPDATA%\LocalEmbeddings\models\`; Linux: `~/.local/share/LocalEmbeddings/models/`) to avoid duplicating path logic.

## Build Verification

`dotnet build` from repo root: **0 warnings, 0 errors** across `net8.0` and `net10.0` for all projects including the new benchmark project.
