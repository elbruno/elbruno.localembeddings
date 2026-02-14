# Changelog

All notable changes to this project are documented in this file.

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
