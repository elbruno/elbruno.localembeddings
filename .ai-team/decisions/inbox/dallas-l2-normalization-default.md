# Decision: L2 Normalization Default

**Date:** 2026-02-13  
**Author:** Dallas (Core Dev)  
**Status:** Implemented

## Context

Added `NormalizeEmbeddings` option to `LocalEmbeddingsOptions` to control whether embeddings are L2-normalized to unit length.

## Decision

**Default is `false` (no normalization)** to maintain backward compatibility.

## Rationale

1. **Breaking change avoidance**: Changing vector magnitudes would affect existing similarity scores
2. **Opt-in behavior**: Users who want sentence-transformers-compatible normalized vectors can enable it
3. **Performance consideration**: Normalization adds a small computational overhead

## Usage Note

When `NormalizeEmbeddings = true`:
- Cosine similarity equals dot product (faster computation in some scenarios)
- Vectors have magnitude 1.0
- Matches Python sentence-transformers default output
