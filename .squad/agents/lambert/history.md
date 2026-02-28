# Project Context

- **Owner:** Bruno Capuano
- **Project:** LocalEmbeddings — a .NET library for local embedding generation using Microsoft.Extensions.AI abstractions
- **Stack:** .NET 10, C#, Microsoft.Extensions.AI, ONNX Runtime, HuggingFace models (all-MiniLM)
- **Created:** 2026-02-12

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-02-12: Test Suite Created
- Created comprehensive unit tests in `tests/LocalEmbeddings.Tests/`
- Test files: `ModelDownloaderTests.cs`, `TokenizerTests.cs`, `LocalEmbeddingGeneratorTests.cs`
- Uses xUnit, Moq for mocking, Xunit.SkippableFact for conditional skipping
- Integration tests marked with `[Trait("Category", "Integration")]` for CI filtering
- Unit tests (non-integration) can run without model files by using mocks
- Run unit tests: `dotnet test --filter "Category!=Integration"`
- Run all tests: `dotnet test` (requires model files in cache)

### Phase 1 Security & Performance Tests (SEC-001, SEC-006, PERF-01, PERF-02)

**Discovery: all Phase 1 implementations were already complete** when tests were written.
- `LocalEmbeddingsOptions.ExpectedHash` was already added by Ash.
- `ModelDownloader.EnsureModelAsync` already had the SEC-006 path guard and SEC-001 sidecar hash logic.
- `OnnxEmbeddingModel.ApplyMeanPooling` already used TensorPrimitives SIMD (PERF-02) and ArrayPool (PERF-01).

**Key SEC-006 insight:** `DefaultPathHelper.SanitizeModelName` from the HuggingFace Downloader package converts `/` to `_`, so slash-based traversal names like `"../../escape"` resolve to safe subdirectories inside the cache. The `Path.GetFullPath` guard in Ash's code fires for inputs that don't contain `/`, like a bare `".."`. Tests should use `".."` (no slash) to directly exercise the guard, and slash-based traversal tests should verify the *property* (no files outside cache) rather than expecting `ArgumentException`.

**Production code changes made to support testing:**
- `OnnxEmbeddingModel.ApplyMeanPooling`: changed from `private static` → `internal static`
- `ElBruno.LocalEmbeddings.csproj`: added `<InternalsVisibleTo Include="ElBruno.LocalEmbeddings.Tests" />`
- `ElBruno.LocalEmbeddings.Tests.csproj`: added `Microsoft.ML.OnnxRuntime` reference for `DenseTensor<float>` in mean pooling tests

**Test files added:**
- `ModelDownloaderSecurityTests.cs` — SEC-006 path traversal guard (11 tests)
- `HashVerificationTests.cs` — SEC-001 sidecar hash verification (12 tests)
- `MeanPoolingTests.cs` — PERF-02 SIMD correctness + PERF-01 regression (8 tests)
- Total: 31 new tests, all passing on net8.0 and net10.0

