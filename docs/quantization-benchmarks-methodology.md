# Quantization Benchmarks — Methodology & Results

## Overview

This document describes the benchmarking methodology for comparing full-precision (FP32) ONNX embedding models against quantized variants (INT8) to measure the speed and accuracy tradeoffs.

**Goal:** Demonstrate that INT8 quantization provides sufficient accuracy preservation (>95% cosine similarity) while delivering meaningful performance gains (>20% speedup) to justify adoption in production embeddings pipelines.

---

## Methodology

### Test Model

- **Model:** `sentence-transformers/all-MiniLM-L6-v2`
- **Task:** Semantic text embeddings
- **Embedding Dimension:** 384
- **Framework:** ONNX Runtime (CPU inference)
- **CPU Execution:** Multi-threaded (inter-op threads = processor count, intra-op threads = processor count)

### Quantization Variants

1. **Full-Precision (FP32)**
   - Original model file: `model.onnx`
   - Precision: Float32 (baseline)

2. **INT8 Quantized**
   - Model file: `model_int8.onnx` or `model_quantized.onnx`
   - Precision: Signed 8-bit integers
   - Quantization approach: Static/dynamic quantization (ONNX Model Quantizer)
   - Expected memory reduction: ~75% (4x smaller)
   - Expected speedup: 15–30% (hardware-dependent)

3. **FP16 Quantized** (future baseline)
   - Model file: `model_fp16.onnx`
   - Precision: Float16
   - Expected memory reduction: ~50% (2x smaller)
   - Expected speedup: 10–20% (depends on hardware SIMD support)

---

## Benchmark Design

### Benchmark Class: `QuantizedVsFullBenchmarks`

Located in: `src/ElBruno.LocalEmbeddings.Benchmarks/QuantizedVsFullBenchmarks.cs`

#### Benchmarks Implemented

1. **Single Embedding Generation**
   - `FullPrecision_SingleEmbedding()` — FP32 inference on 1 sentence
   - `Quantized_SingleEmbedding()` — INT8 inference on 1 sentence
   - **Metric:** Latency per embedding (milliseconds)

2. **Batch of 10 Embeddings**
   - `FullPrecision_Batch10()` — FP32 inference on 10 sentences
   - `Quantized_Batch10()` — INT8 inference on 10 sentences
   - **Metric:** Total latency & amortized per-embedding latency

3. **Batch of 32 Embeddings**
   - `FullPrecision_Batch32()` — FP32 inference on 32 sentences
   - `Quantized_Batch32()` — INT8 inference on 32 sentences
   - **Metric:** Batch throughput optimization pattern

4. **Batch of 100 Embeddings**
   - `FullPrecision_Batch100()` — FP32 inference on 100 sentences
   - `Quantized_Batch100()` — INT8 inference on 100 sentences
   - **Metric:** Peak throughput and sustained performance

#### Accuracy Metrics

- **Cosine Similarity:** Computed between full-precision and quantized embeddings on the same input
- **Target Threshold:** >0.95 (5% maximum vector distance)
- **Formula:**
  ```
  similarity = dot(a, b) / (||a|| * ||b||)
  ```
- **Interpretation:**
  - 1.0 = identical embeddings
  - >0.95 = negligible accuracy loss, acceptable for most semantic search tasks
  - <0.95 = material accuracy loss; quantization may not be suitable for the model

#### Memory Diagnostics

- **BenchmarkDotNet `[MemoryDiagnoser]` attribute** captures:
  - Bytes allocated per operation
  - Gen0/Gen1/Gen2 garbage collection events
  - Peak working set size at model load time

---

## Test Data

**Test Sentences:**
```csharp
"The quick brown fox jumps over the lazy dog."
"Sample text 0 for quantization benchmarking."
"Sample text 1 for quantization benchmarking."
... (up to 100 samples)
```

- **Rationale:** Short, diverse sentences representative of semantic search queries
- **Length:** 5–15 tokens, matching real-world embeddings use cases

---

## Execution Environment

### Prerequisites

1. Models cached locally at:
   - Windows: `%LOCALAPPDATA%\LocalEmbeddings\models\sentence-transformers_all-MiniLM-L6-v2\`
   - Linux: `~/.local/share/LocalEmbeddings/models/sentence-transformers_all-MiniLM-L6-v2/`

2. Both `model.onnx` and `model_int8.onnx` (or `model_quantized.onnx`) present in the model directory

### BenchmarkDotNet Configuration

```csharp
// Default configuration (Release mode)
dotnet run --configuration Release --project src/ElBruno.LocalEmbeddings.Benchmarks
```

**Output:**
- Console summary table (quick overview)
- HTML report: `BenchmarkDotNet.Artifacts/results/QuantizedVsFullBenchmarks-report.html`
- JSON raw data: `BenchmarkDotNet.Artifacts/results/QuantizedVsFullBenchmarks-report-github.json`

### Warm-up & Iteration Strategy

- **Warm-up:** 3 iterations (ONNX Runtime session initialization, JIT compilation)
- **Target:** 100 iterations (statistical stability)
- **Measurement:** Median latency reported (robust to outliers)

---

## Success Criteria — Quantization Phase 1 (Quick Win)

✅ **Accuracy Preservation:**
- Cosine similarity between full-precision and INT8 embeddings: **≥0.95**
- If < 0.95: Model may require fine-tuning or selective quantization

✅ **Performance Improvement:**
- INT8 latency: **≤ 80% of FP32 latency** (≥20% speedup)
- Applies to single embeddings and all batch sizes tested

✅ **Memory Efficiency:**
- INT8 model file size: **≤ 30% of FP32 model size**
- Runtime heap allocations: Should not exceed FP32 baseline by >10%

---

## Results Interpretation

### Case 1: High Accuracy, High Speed ✅
```
Cosine Similarity: 0.98
INT8 Latency: 60% of FP32
Decision: ADOPT INT8 as default; FP32 optional
```

### Case 2: Good Accuracy, Modest Speed
```
Cosine Similarity: 0.96
INT8 Latency: 85% of FP32
Decision: OFFER INT8 as opt-in; document tradeoff
```

### Case 3: Acceptable Accuracy, Low Speed
```
Cosine Similarity: 0.92
INT8 Latency: 70% of FP32
Decision: REJECT for production; revisit quantization method
```

---

## Running the Benchmarks

### Quick Run (Development)
```bash
cd src/ElBruno.LocalEmbeddings.Benchmarks
dotnet run --configuration Release -- --filter "*QuantizedVsFullBenchmarks*"
```

### Full Run (Publication)
```bash
cd src/ElBruno.LocalEmbeddings.Benchmarks
dotnet run --configuration Release
```

### Extract Results
```bash
# HTML report path
cat BenchmarkDotNet.Artifacts/results/QuantizedVsFullBenchmarks-report.html

# Summary metrics
grep "| Quantized\|| FullPrecision" BenchmarkDotNet.Artifacts/results/*.txt
```

---

## Future Work

- [ ] FP16 quantization variant (half-precision)
- [ ] GPU inference benchmarks (CUDA/TensorRT)
- [ ] Larger model variants (all-MiniLM-L12-v2, e5-small, etc.)
- [ ] Accuracy preservation across diverse semantic tasks (clustering, ranking)
- [ ] Automated quantization for new models (GitHub Actions workflow)

---

## References

- [ONNX Model Quantizer](https://github.com/microsoft/onnxruntime/tree/main/onnxruntime/quantization)
- [BenchmarkDotNet Documentation](https://benchmarkdotnet.org/)
- [Quantization-Aware Training (QAT) for Embeddings](https://arxiv.org/abs/2004.07159)
- [Sentence Transformers Model Card](https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2)

---

## Revision History

| Date       | Version | Changes                                                    |
|------------|---------|-------------------------------------------------------------|
| 2026-05-19 | 1.0     | Initial methodology doc; single & batch benchmarks added   |
