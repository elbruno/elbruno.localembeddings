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

### Phase 3 Performance Tests (PERF-09, PERF-08, PERF-12/13)

**PERF-09 (`FindClosest` heap):**
- Created `tests/ElBruno.LocalEmbeddings.Tests/FindClosestTests.cs` (new file, 9 unit tests + 3 integration tests).
- Key design: parity tests generate a deterministic corpus (fixed `Random(42)` seed, 100-200 items) and compare `FindClosest` output against an inline LINQ reference implementation — ensures heap result is byte-for-byte identical to the sorted LINQ reference.
- Edge cases covered: topK > corpus size, topK=1, empty corpus, all-equal scores (distinctness of returned indices enforced), partial corpus with minScore filter.
- The `FindClosest` signature is `(Embedding<float> query, IReadOnlyList<Embedding<float>> corpus, int topK, float? minScore)` — tests call it as a public extension method via `using ElBruno.LocalEmbeddings.Extensions`.

**PERF-08/12/13 (tokenizer allocation regression):**
- Added 3 `[SkippableFact]` + `[Trait("Category", "Integration")]` tests that verify: (1) special-token layout for a known input, (2) deterministic output across two calls to the same instance, (3) batch output matches individual `Tokenize` calls row-by-row.
- All three skip cleanly when no model files are available (follows existing tokenizer test pattern).

**Test counts (non-integration):** 9 new `[Fact]`/`[Theory]` tests, 0 failures, 0 warnings on both net8.0 and net10.0.

**Run command verified:** `dotnet test tests/ElBruno.LocalEmbeddings.Tests/ --filter "Category!=Integration"` → 99 passed.

### Phase 2 Security Tests (SEC-003, SEC-004, SEC-005)

**All Phase 2 implementations (Ash) were complete** before tests were written; tests verified them immediately.

**Key insights:**

- **SEC-003 (`ImageEmbeddingsOptions` filename validation):** `ValidateFileName` checks: (1) not null/whitespace, (2) no `..` sequence, (3) no chars in `Path.GetInvalidFileNameChars()`. Tests use `[Theory]` with representative bad inputs; `..evil` (no slash) is a good minimal traversal test case. Invalid-char tests use `<`, `>`, `|`, `?`, `*` — all invalid on Windows.

- **SEC-004 (CLIP encoder constructors):** `ArgumentException.ThrowIfNullOrWhiteSpace` throws `ArgumentNullException` for null and `ArgumentException` for empty/whitespace. Use `Assert.ThrowsAny<ArgumentException>` to cover both. `FileNotFoundException` tests need a path whose parent directory does not exist (guaranteed non-existent). Verify `ClipTextEncoder` vocabPath check by creating a real zero-byte model file so modelPath passes, then provide a missing vocabPath.

- **SEC-005 (`ImageSearchEngine` null guards):** Constructor null check for `imageEncoder` is testable without ONNX files (pass `null!`; check fires before any I/O). `textEncoder` null check and all `SearchByText` guard tests require a live engine, which needs real ONNX model files → use `SkippableFact` + env vars `CLIP_VISION_MODEL_PATH`, `CLIP_TEXT_MODEL_PATH`, `CLIP_VOCAB_PATH`, `CLIP_MERGES_PATH`.

- **PERF-03/15/16 (Parker):** No direct tests needed; verified by running full build and existing test suite with 0 failures.

**Test files added (ImageEmbeddings test project):**
- `ImageEmbeddingsOptionsValidationTests.cs` — SEC-003, 18 tests (all pass)
- `ClipEncoderConstructorTests.cs` — SEC-004, 10 tests (all pass)
- `ImageSearchEngineNullGuardTests.cs` — SEC-005, 6 tests (1 passes, 5 skipped pending real ONNX files)
- **Total new tests: 34 — 29 pass, 5 skip on both net8.0 and net10.0 (0 failures)**

