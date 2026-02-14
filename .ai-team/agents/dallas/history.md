# Project Context

- **Owner:** Bruno Capuano (bcapuano@gmail.com)
- **Project:** LocalEmbeddings — a .NET library for local embedding generation using Microsoft.Extensions.AI abstractions
- **Stack:** .NET 10, C#, Microsoft.Extensions.AI, ONNX Runtime, HuggingFace models (all-MiniLM)
- **Created:** 2026-02-12

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-02-12: ModelDownloader Implementation

- `src/LocalEmbeddings/ModelDownloader.cs` — Downloads and caches ONNX models from HuggingFace Hub
- `IModelDownloader` interface added for testability (DI-friendly)
- Cache locations:
  - Windows: `%LOCALAPPDATA%\LocalEmbeddings\models\`
  - Linux/macOS: `~/.local/share/LocalEmbeddings/models/` (respects XDG_DATA_HOME)
- Downloads: `model.onnx` from `/onnx/` path, plus tokenizer files (`tokenizer.json`, `tokenizer_config.json`, `vocab.txt`)
- Uses streaming downloads with temp files to prevent partial downloads
- Progress reporting via `IProgress<double>` (0.0 to 1.0)

### 2026-02-12: OnnxEmbeddingModel Implementation

- `src/LocalEmbeddings/OnnxEmbeddingModel.cs` — Handles ONNX model inference for embeddings
- Class made `public` for external consumption
- **Load method:**
  - Creates `InferenceSession` with performance optimizations (`GraphOptimizationLevel.ORT_ENABLE_ALL`, parallel execution)
  - Uses all available CPU cores (`Environment.ProcessorCount`)
  - Validates model exists, prevents double-loading
  - Extracts embedding dimension from model output metadata
- **GenerateEmbedding (single):**
  - Takes `long[]` inputIds and attentionMask
  - Delegates to batched version for code reuse
- **GenerateEmbeddings (batch):**
  - Creates `DenseTensor<long>` for `input_ids`, `attention_mask`, and optional `token_type_ids`
  - Validates all sequences have same length (required for batching)
  - Runs inference via `InferenceSession.Run()` — thread-safe for concurrent calls
  - Applies **mean pooling** over sequence dimension, weighted by attention mask
- **Thread safety:** `InferenceSession.Run()` is documented as thread-safe; documented in class remarks

### 2026-02-12: Tokenizer Implementation

- `src/LocalEmbeddings/Tokenizer.cs` — Wraps Microsoft.ML.Tokenizers for HuggingFace compatibility
- Uses `BertTokenizer.Create(stream)` to load from `tokenizer.json` files
- **Tokenize method:**
  - Accepts text and optional maxLength (default 512)
  - Returns `(long[] InputIds, long[] AttentionMask)` tuple
  - Handles padding to fixed length, attention mask reflects actual tokens vs padding
- **TokenizeBatch method:**
  - Tokenizes multiple texts at once
  - All outputs padded to same length for batched inference
- Special token IDs exposed: `PaddingTokenId`, `ClassificationTokenId`, `SeparatorTokenId`
- Thread-safe after initialization

### 2026-02-13: Quick Wins Implementation

Four high-value, low-effort improvements implemented:

1. **EmbeddingExtensions** (`src/LocalEmbeddings/Extensions/EmbeddingExtensions.cs`):
   - `CosineSimilarity(ReadOnlyMemory<float> a, ReadOnlyMemory<float> b)` — extension method for similarity calculation
   - `CosineSimilarity(Embedding<float> a, Embedding<float> b)` — convenience overload
   - `FindClosest<T>(IEnumerable<(T, Embedding<float>)>, query, topK, minScore)` — semantic search helper
   - Extracts duplicated similarity logic from samples into reusable API

2. **L2 Normalization Option**:
   - Added `NormalizeEmbeddings` property to `LocalEmbeddingsOptions` (default: false)
   - When enabled, embeddings are L2-normalized to unit length after mean pooling
   - Matches sentence-transformers default behavior; enables dot-product similarity

3. **CreateAsync() Factory**:
   - Added `LocalEmbeddingGenerator.CreateAsync(options, cancellationToken)` static method
   - Wraps constructor in `Task.Run()` for non-blocking initialization
   - Useful in async contexts where blocking constructor is problematic

4. **Metadata via GetService**:
   - Updated `GetService<TService>()` to return `Metadata` when `TService` is `EmbeddingGeneratorMetadata`
   - Allows accessing metadata through `IEmbeddingGenerator` interface without casting
