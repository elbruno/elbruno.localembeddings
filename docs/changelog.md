# Changelog

All notable changes to this project are documented in this file.

## [Unreleased] - 2026-02-28

### Added

- `ElBruno.LocalEmbeddings.Harrier` package — Microsoft Harrier-OSS-v1 support (270M, 640-dim, 94+ languages)
- Shared multilingual test suite (`SharedModelTests`) covering 10 languages for both base and Harrier models
- `HarrierConsoleApp` sample demonstrating Harrier model usage, batching, similarity, and token counting
- `docs/harrier-integration.md` — Complete Harrier integration guide with instruction prefix examples and configuration
- Harrier to NuGet publishing workflow and CI/CD coverage
- SHA-256 sidecar integrity verification for ONNX model files and .onnx_data companions
- Concurrent download serialization to prevent race conditions during model cache population
- File size guards (50 MB limit) for tokenizer files to prevent resource exhaustion
- `LocalEmbeddingGenerator` now implements `IAsyncDisposable` (`DisposeAsync`).
- `LocalEmbeddingGenerator.CountTokens(string)` for tokenizer-backed token counting.
- `LocalEmbeddingTextGenerator` now implements `IAsyncDisposable`.
- `KernelMemoryBuilderExtensions.WithLocalEmbeddingsSearchOnly(...)` convenience overloads for retrieval/search-only scenarios.
- New unit tests:
  - Kernel Memory adapter behavior (`GenerateEmbeddingAsync`, tokenization behavior, ownership/disposal).
  - Core DI registration overloads for `AddLocalEmbeddings(...)`.
  - Direct `OnnxEmbeddingModel` guard/validation tests.

### Changed

- Fixed `HttpClient` lifetime usage in `LocalEmbeddingGenerator` model-resolution paths by using a reusable shared client instead of creating new instances per call.
- Improved cancellation propagation by forwarding `CancellationToken` through tokenization and batched inference paths.
- Improved `LocalEmbeddingTextGenerator.CountTokens(...)` to use tokenizer-backed counting automatically when wrapping `LocalEmbeddingGenerator`.
- Updated docs (`README`, getting started, configuration, API reference, DI, Kernel Memory integration) to reflect async-first initialization and search-only Kernel Memory usage.
- Migrated `src/src/Samples/RagChat` from a sample-local vector store implementation to shared `ElBruno.LocalEmbeddings.VectorData.InMemory` via `AddLocalEmbeddingsWithInMemoryVectorStore(...)`.
- Removed duplicate sample-only in-memory vector store code from `src/src/Samples/RagChat/VectorStore` and aligned docs to position RagChat as the in-memory VectorData reference sample.
- Optimized `CountTokens` implementations to avoid unnecessary allocations.
- Fixed allocation patterns in HarrierTokenizer and base tokenization paths.
- Added SentencePiece normalization for proper BPE tokenization in Harrier.

### Fixed

- `HarrierTokenizer`: Fixed maxLength=1 index-out-of-bounds bug in start-of-sequence handling.
- `.onnx_data` companion file not verified on cache hit — now included in SHA-256 sidecar verification.

## [Unreleased] - 2026-02-14

### Added

- `LocalEmbeddingGenerator` now implements `IAsyncDisposable` (`DisposeAsync`).
- `LocalEmbeddingGenerator.CountTokens(string)` for tokenizer-backed token counting.
- `LocalEmbeddingTextGenerator` now implements `IAsyncDisposable`.
- `KernelMemoryBuilderExtensions.WithLocalEmbeddingsSearchOnly(...)` convenience overloads for retrieval/search-only scenarios.
- New unit tests:
  - Kernel Memory adapter behavior (`GenerateEmbeddingAsync`, tokenization behavior, ownership/disposal).
  - Core DI registration overloads for `AddLocalEmbeddings(...)`.
  - Direct `OnnxEmbeddingModel` guard/validation tests.

### Changed

- Fixed `HttpClient` lifetime usage in `LocalEmbeddingGenerator` model-resolution paths by using a reusable shared client instead of creating new instances per call.
- Improved cancellation propagation by forwarding `CancellationToken` through tokenization and batched inference paths.
- Improved `LocalEmbeddingTextGenerator.CountTokens(...)` to use tokenizer-backed counting automatically when wrapping `LocalEmbeddingGenerator`.
- Updated docs (`README`, getting started, configuration, API reference, DI, Kernel Memory integration) to reflect async-first initialization and search-only Kernel Memory usage.
- Migrated `src/src/Samples/RagChat` from a sample-local vector store implementation to shared `ElBruno.LocalEmbeddings.VectorData.InMemory` via `AddLocalEmbeddingsWithInMemoryVectorStore(...)`.
- Removed duplicate sample-only in-memory vector store code from `src/src/Samples/RagChat/VectorStore` and aligned docs to position RagChat as the in-memory VectorData reference sample.

