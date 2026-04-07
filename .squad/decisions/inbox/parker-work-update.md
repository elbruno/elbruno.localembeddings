# Parker Work Update — Harrier Benchmarks + Cleanup Sprint

**By:** Parker (Performance Engineer)  
**Date:** 2025-07-17  
**Status:** Complete — all 4 items done, build clean

---

## Changes Made

### 1. Harrier Benchmarks (perf-harrier-benchmarks)

**New files:**
- `benchmarks/ElBruno.LocalEmbeddings.Benchmarks/HarrierTokenizerBenchmarks.cs` — 6 benchmarks (short/long text, batch-10, with/without prefix, CountTokens)
- `benchmarks/ElBruno.LocalEmbeddings.Benchmarks/HarrierEmbeddingBenchmarks.cs` — 3 benchmarks (single, batch-10, batch-100)
- `benchmarks/ElBruno.LocalEmbeddings.Benchmarks/HarrierVsBaseBenchmarks.cs` — 2 benchmarks (base MiniLM vs Harrier head-to-head)

**Modified files:**
- `benchmarks/ElBruno.LocalEmbeddings.Benchmarks/BenchmarkHelpers.cs` — added `TryResolveHarrierModelDirectory()`, refactored cache dir helpers
- `benchmarks/ElBruno.LocalEmbeddings.Benchmarks/ElBruno.LocalEmbeddings.Benchmarks.csproj` — added Harrier project reference

**CI safety:** All 11 new benchmarks no-op gracefully when Harrier model is not cached locally. Same nullable guard pattern as existing benchmarks.

### 2. slnx Cleanup (cleanup-slnx)

Added `samples/DocumentRagFoundry/DocumentRagFoundry.csproj` to solution file.

### 3. NPU Directory Cleanup (cleanup-npu-dirs)

Removed 6 empty NPU directories (contained only build artifacts, no source):
- `src/ElBruno.LocalEmbeddings.Npu/`
- `src/ElBruno.LocalEmbeddings.Npu.Intel/`
- `src/ElBruno.LocalEmbeddings.Npu.Qualcomm/`
- `tests/ElBruno.LocalEmbeddings.Npu.Tests/`
- `tests/ElBruno.LocalEmbeddings.Npu.Intel.Tests/`
- `tests/ElBruno.LocalEmbeddings.Npu.Qualcomm.Tests/`

### 4. OnnxRuntime 1.24.2 → 1.24.4 (cleanup-onnxruntime-bump)

Updated `Microsoft.ML.OnnxRuntime` in 4 csproj files (2 src, 2 test).

---

## Build Verification

- `dotnet build` — **0 warnings, 0 errors** (all frameworks: net8.0 + net10.0)
- `dotnet test` — **0 failures** across all test projects

## Notes for Team

- **Dallas:** If you're adding an explicit OnnxRuntime reference to the Harrier csproj, use version 1.24.4 to match the bump across the solution.
- **Benchmark runners:** Harrier benchmarks require the model cached at `%LOCALAPPDATA%\ElBruno\LocalEmbeddings\models\onnx-community_harrier-oss-v1-270m-ONNX`. Run `HarrierConsoleApp` once to trigger the download.
- **Harrier tokenizer perf note:** The default `MaxSequenceLength=8192` allocates ~128 KB of `long[]` per Tokenize() call. The new `HarrierTokenizerBenchmarks` will quantify this precisely once a model is available — first data point for PERF-HIGH-1 remediation.
