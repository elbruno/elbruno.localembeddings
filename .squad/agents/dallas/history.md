# Project Context

- **Owner:** Bruno Capuano
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

### 2026-02-28: Harrier Package Code Review

- Reviewed full `src/ElBruno.LocalEmbeddings.Harrier/` implementation against base library patterns
- **HarrierOnnxEmbeddingModel:** ArrayPool + tensor construction correct. Missing Linux ONNX alias workaround and DllNotFoundException handling from base library.
- **HarrierTokenizer:** Manual BPE tokenizer construction from tokenizer.json. KEY RISK: SentencePiece normalizer (space→▁) may not be applied by `BpeTokenizer.Create`. Index-out-of-bounds bug when maxLength=1.
- **HarrierModelDownloader:** Missing concurrent download serialization (base uses `ConcurrentDictionary<SemaphoreSlim>`). No sidecar hash verification on cache hit (SEC-001 gap). `.onnx_data` companion not checked on cache hit.
- **HarrierEmbeddingGenerator:** Clean async factory pattern. No sync constructor (better than base). DI path still has sync-over-async risk.
- **Code duplication candidates:** SHA-256 helpers, path traversal guards, SessionOptions construction, GenerateAsync boilerplate — all duplicated between base and Harrier.
- Full report written to `.squad/decisions/inbox/dallas-harrier-code-review.md`

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

### 2026-03-01: Harrier Hardening and Parity Fixes

- Harrier tokenizer now applies SentencePiece normalization, enforces maxLength >= 3, adds tokenizer.json size guard, and optimizes CountTokens to avoid padded allocations.
- Harrier model downloader now serializes concurrent downloads, verifies sidecar hashes on cache hit, checks .onnx_data, writes sidecars for data files, and avoids double SHA-256 work.
- Harrier ONNX loader adds Linux alias workaround and DllNotFoundException diagnostics; shared HttpClient uses pooled lifetime; provider name fixed; explicit package refs added.
### 2026-02-13: Package Dependency Updates (April 2026 Latest)

Updated all NuGet package references to their latest stable versions as of April 2026:

**Core Library Packages:**
- `Microsoft.Extensions.AI.Abstractions`: 10.3.0 → 10.4.1
- `Microsoft.ML.OnnxRuntime`: 1.24.3 → 1.24.4 (all variants: base, DirectML, QNN)
- `System.Numerics.Tensors`: 9.0.3 → 10.0.5
- `ElBruno.HuggingFace.Downloader`: 0.5.0 → 0.6.0
- All `Microsoft.Extensions.*` packages: 10.0.3 → 10.0.5
- `Microsoft.Extensions.VectorData.Abstractions`: 9.7.0 → 10.1.0

**Test Packages:**
- `Microsoft.NET.Test.Sdk`: 17.14.1 → 18.3.0
- `coverlet.collector`: 6.0.4 → 8.0.1
- `xunit.runner.visualstudio`: 3.1.4 → 3.1.5
- `Xunit.SkippableFact`: 1.5.23 → 1.5.61

**Sample/Benchmark Packages:**
- `BenchmarkDotNet`: 0.14.0 → 0.15.8
- `Spectre.Console`: 0.49.1 → 0.55.0
- `OllamaSharp`: 5.4.16 → 5.4.25
- `OpenAI`: 2.8.0 → 2.10.0
- `Microsoft.Agents.AI`: 1.0.0-rc1 → 1.0.0
- `Microsoft.Extensions.AI.OpenAI`: 10.3.0 → 10.4.1
- `System.Management`: 9.0.3 → 10.0.5

**Important Notes:**
- `Microsoft.AI.Foundry.Local` kept at 0.1.0 (not updated to 0.9.0) due to breaking API changes — version 0.9.0 removed the `StartModelAsync` method that the RagFoundryLocal sample depends on
- `Intel.ML.OnnxRuntime.OpenVino` kept at 1.24.1 (uses separate versioning from main ORT packages due to standalone runtime)
- All updates verified: solution builds successfully, all 138 tests pass across both net8.0 and net10.0 targets
- Package version updates grouped in commits with Ripley (core/test packages) and Dallas (sample/benchmark packages)

### 2026-02-13: Batch and Streaming Embeddings APIs

Added two new high-value features to the core library for efficient large-scale embedding generation:

1. **Batch Embedding API with Progress Reporting** (Feature 1.1):
   - Created `EmbeddingProgress` record in `src/ElBruno.LocalEmbeddings/EmbeddingProgress.cs`
   - Record type with properties: `CompletedItems`, `TotalItems`, `CurrentBatchSize`
   - Added extension method `GenerateAsync(IEnumerable<string>, IProgress<EmbeddingProgress>, batchSize, options, cancellationToken)`
   - Splits input into configurable batches (default 32), reports progress after each batch completes
   - Returns aggregated `GeneratedEmbeddings<Embedding<float>>` with all results in input order
   - Useful for monitoring long-running embedding operations on large datasets

2. **Streaming Embeddings API** (Feature 1.2):
   - Added extension method `GenerateStreamingAsync(IEnumerable<string>, batchSize, options, cancellationToken)`
   - Returns `IAsyncEnumerable<Embedding<float>>` for processing embeddings as they become available
   - Uses `[EnumeratorCancellation]` attribute for proper async enumerable cancellation support
   - Enables streaming processing without waiting for all embeddings to complete
   - Embeddings yielded in input order as each batch is processed

**Implementation Details:**
- Both methods use `Chunk(batchSize)` from System.Linq for efficient batching
- Both support cancellation via `CancellationToken.ThrowIfCancellationRequested()`
- All parameters validated with proper `ArgumentNullException` and `ArgumentOutOfRangeException` checks
- Comprehensive XML documentation with usage examples
- Added `using System.Runtime.CompilerServices;` for `[EnumeratorCancellation]` attribute

**Compilation Fixes:**
- Fixed metadata access in new WIP files (`CachingEmbeddingDecorator`, `EmbeddingComparer`) to use `GetService<EmbeddingGeneratorMetadata>()` pattern
- Added AOT/trimming compatibility attributes to `ServiceCollectionExtensions.AddLocalEmbeddings(IConfiguration)` method
- Used fully qualified attribute names: `[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode]` and `[System.Diagnostics.CodeAnalysis.RequiresDynamicCode]`

**Verification:**
- Project builds successfully on both net8.0 and net10.0 targets
- No warnings (TreatWarningsAsErrors is enabled)

### 2026-04-07: HarrierMultilingualSample Created

Created comprehensive multilingual sample at `samples/HarrierMultilingualSample/` demonstrating Harrier's 94+ language support:

**Showcase A — Cross-lingual retrieval:**
- English knowledge base with 12 diverse facts (science, history, geography, tech, culture, food, sports)
- 7 multilingual queries: Spanish, French, German, Portuguese, Japanese, Chinese, Arabic
- Demonstrates accurate retrieval of English facts from non-English queries
- Results table shows query language, original query, matched English fact, similarity score

**Showcase B — Language-agnostic search:**
- 8-fact multilingual knowledge base, each fact in different language (Spanish, French, German, Portuguese, Italian, Japanese, Korean, Russian)
- 8 English queries designed to match each fact
- Demonstrates bidirectional multilingual semantic search
- Results table shows query/document language pairs and similarity scores

**Implementation Pattern:**
- Two separate generators created: one without instruction prefix (for documents), one with prefix (for queries)
- Follows established HarrierConsoleApp pattern: top-level statements, CreateAsync factory, progress reporting
- Custom CosineSimilarity function for semantic search
- Unicode box-drawing characters for clean console UI
- Builds cleanly with 0 warnings, 0 errors (TreatWarningsAsErrors enabled)

**Key Learnings:**
- Harrier instruction-tuning requires different prefix handling for documents vs queries
- Multi-generator pattern is the clean solution when needing both prefix and non-prefix embeddings
- Multilingual RAG requires no special handling — same API, just different language inputs

### 2026-04-08: DirectML GPU Support for ElBruno.LocalEmbeddings.Harrier

Added DirectML execution provider support on branch `feature/harrier-gpu-directml`.

**Package changes (`ElBruno.LocalEmbeddings.Harrier.csproj`):**
- Replaced `Microsoft.ML.OnnxRuntime` with platform-conditional references:
  - `Microsoft.ML.OnnxRuntime.DirectML` 1.24.4 on Windows (`$(OS) == 'Windows_NT'`)
  - `Microsoft.ML.OnnxRuntime` 1.24.4 on non-Windows
- Added `DIRECTML` preprocessor constant via a `Condition`-based `PropertyGroup` on Windows

**Options (`HarrierEmbeddingsOptions.cs`):**
- Added `UseDirectML` (bool, default `false`) — enables DirectML GPU acceleration on Windows
- Added `DirectMLDeviceId` (int, default `0`) — selects the GPU device when DirectML is enabled

**Model loading (`HarrierOnnxEmbeddingModel.Load`):**
- Extended signature with `useDirectML` and `directMLDeviceId` parameters (both defaulted)
- Added `#if DIRECTML` block calling `sessionOptions.AppendExecutionProvider_DML(deviceId)` before creating the session
- Broadened exception filter from `DllNotFoundException` only to `ex is DllNotFoundException or TypeInitializationException` — matches base library pattern

**Generator (`HarrierEmbeddingGenerator`):**
- Updated `_model.Load()` call to pass `options.UseDirectML` and `options.DirectMLDeviceId`

**Samples:**
- `HarrierMultilingualSample`: detects Windows via `RuntimeInformation.IsOSPlatform`, sets `UseDirectML = useGpu` on both doc and query options, shows platform + acceleration in header, updates ✓ ready lines with GPU/CPU label
- `HarrierConsoleApp`: same GPU detection pattern, adds `UseDirectML = useGpu` to options, prints acceleration line in setup block

**Key decisions:**
- `#if DIRECTML` guard ensures `AppendExecutionProvider_DML` is never compiled on Linux/macOS where the method doesn't exist in the CPU-only package
- `DefineConstants` appended (not replaced) to preserve any existing constants
- Samples default `useGpu = isWindows` so they run GPU automatically on Windows without requiring manual config

