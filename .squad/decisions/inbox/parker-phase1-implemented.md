# Decision Note: Phase 1 Performance Fixes Implemented

**By:** Parker (Performance Engineer)  
**Date:** 2025-07-16  
**Status:** Implemented — build verified ✅

---

## Changes Made

**File:** `src/ElBruno.LocalEmbeddings/OnnxEmbeddingModel.cs`

### PERF-02: Mean Pooling — SIMD via TensorPrimitives

The `ApplyMeanPooling` method previously used a triple-nested scalar loop iterating over `[batch, seq, hidden]`. It now:

1. Casts `Tensor<float>` → `DenseTensor<float>` (safe: ORT always returns DenseTensor) and takes a flat `Span<float>` via `.Buffer.Span`.
2. For each unmasked `(batch, seq)` token, computes the flat offset `(batch * sequenceLength + seq) * hiddenSize` and calls `TensorPrimitives.Add(embedding, tensorSpan.Slice(offset, hiddenSize), embedding)`.
3. Divides the accumulated embedding by `tokenCount` with `TensorPrimitives.Divide(embedding, (float)tokenCount, embedding)`.

This eliminates the inner `hidden` scalar loop entirely, replacing it with hardware-vectorized SIMD operations.

### PERF-01: ArrayPool for Flattening Arrays

The three `new long[batchSize * sequenceLength]` allocations per call for `flatInputIds`, `flatAttentionMask`, and `flatTokenTypeIds` were replaced with `ArrayPool<long>.Shared.Rent(totalSize)` wrapped in `try/finally`.

Key correctness details:
- Arrays sliced to exact size via `.AsMemory(0, totalSize)` when constructing `DenseTensor<long>`.
- `flatTokenTypeIds` explicitly zero-cleared before use (rented memory is not guaranteed zero).
- All three arrays returned in `finally` — safe on both normal exit and exception paths.

---

## Expected Impact

| Fix | Metric | Expected Improvement |
|-----|--------|---------------------|
| PERF-02 | Mean pooling throughput | Significant (vectorized vs. scalar inner loop) |
| PERF-01 | GC pressure (batch=100, seq=512) | ~1.2 MB fewer allocations per call |

---

## Constraints Respected

- Public API unchanged (no signature changes)
- `using TensorPrimitives = System.Numerics.Tensors.TensorPrimitives;` alias preserved
- `nullable enable` maintained, no new warnings
- `dotnet build` passes cleanly
