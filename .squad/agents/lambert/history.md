# Project Context

- **Owner:** Bruno Capuano
- **Project:** LocalEmbeddings — a .NET library for local embedding generation using Microsoft.Extensions.AI abstractions
- **Stack:** .NET 10, C#, Microsoft.Extensions.AI, ONNX Runtime, HuggingFace models (all-MiniLM)
- **Created:** 2026-02-12

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### Phase 4 Security & Async Tests (SEC-002, SEC-007, SEC-009, PERF-04/05)

**Key findings:**

- **SEC-002 (`ModelDownloader()` default ctor):** Guard test only checks that construction succeeds and `GetCacheDirectory()` returns a non-empty string — private `SocketsHttpHandler` fields are not introspectable. Appended to `ModelDownloaderSecurityTests.cs`.

- **SEC-009 (`ClipTokenizer` file size guard):** Implementation was NOT present in the production code at time of writing; the guard (`>50MB → InvalidOperationException`) has not yet been added to `ClipTokenizer.cs`. Test `ClipTokenizer_OversizedVocabFile_ThrowsInvalidOperationException` is a TDD test that will fail until Ash adds the guard. Test `ClipTokenizer_ValidSizeVocabFile_DoesNotThrowOnSizeCheck` is forward-compatible: it checks that the size guard message doesn't appear, without asserting on unrelated parse exceptions.

- **SEC-007 / PERF-04 (async factory + DI):** `LocalEmbeddingGenerator.CreateAsync` already has three overloads. Reflection approach uses `GetMethods` (not `GetMethod`) to avoid `AmbiguousMatchException`. DI test checks service registration in `IServiceCollection` without calling `BuildServiceProvider`, so no model files are needed.

- **Build validation:** `dotnet build --no-incremental` passes with 0 errors and 0 warnings for all three new/modified test files.

**Test files added/modified:**
- `ModelDownloaderSecurityTests.cs` — appended 1 test for SEC-002
- `tests/ElBruno.LocalEmbeddings.ImageEmbeddings.Tests/ClipTokenizerFileSizeTests.cs` — 2 new tests for SEC-009
- `tests/ElBruno.LocalEmbeddings.Tests/AsyncPatternTests.cs` — 2 new tests for SEC-007/PERF-04

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

### 2026-02-28: Cross-Agent Update — Ash Phase 4 Security Complete

**From:** Scribe (cross-agent propagation)

All 9 security findings from Ash's audit are now fully resolved:
- **SEC-009 (`ClipTokenizer` file size guard):** Ash implemented the 50 MB guard in `ClipTokenizer.cs` (Phase 4). The TDD test `ClipTokenizer_OversizedVocabFile_ThrowsInvalidOperationException` that was written as a failing test in Phase 4 now passes. Guard throws `InvalidOperationException` with file name and actual size in MB.
- **SEC-002 (`ModelDownloader` SocketsHttpHandler):** Implemented by Ash in Phase 4. The behavioral test `ModelDownloader_DefaultConstructor_UsesSocketsHttpHandler` verifies construction succeeds — private handler fields remain not introspectable.
- All SEC-001 through SEC-009 are resolved and test-covered.

### 2026-02-28: Cross-Agent Update — Parker Phase 3/4 Performance Complete

**From:** Scribe (cross-agent propagation)

Parker completed all actionable performance phases:
- **PERF-09/10 (heap search):** Both `FindClosest` overloads and `ImageSearchEngine.RankResults` use O(n log k) `PriorityQueue` min-heaps. Lambert's `FindClosestTests.cs` parity tests (9 unit, deterministic Random(42) seed) confirm heap output is byte-for-byte identical to the LINQ reference.
- **PERF-12/13 pattern:** `as IList<T> ?? .ToList()` is now applied in `TokenizeBatch`, `GenerateAsync`, and confirmed clean by Lambert's tokenizer regression tests.
- **Phase 5 (benchmarks):** Parker is expanding `samples/BenchmarkSample/` with 7 missing benchmark classes (cold start, mean pooling, CLIP encoders, VectorStore search, quantized comparison). No test work needed from Lambert for benchmark infrastructure.

### 2026-04-04: Wave 1 Feature Tests Complete

Wrote comprehensive tests for all Wave 1 features implemented by Dallas in the core library:

**Test files added (6 files, 67 new tests):**
- `BatchEmbeddingTests.cs` — 10 tests for batch API with progress reporting (IProgress<EmbeddingProgress>)
- `StreamingEmbeddingTests.cs` — 10 tests for async streaming API (IAsyncEnumerable<Embedding<float>>)
- `CachingEmbeddingDecoratorTests.cs` — 12 tests for LRU cache (hit/miss/eviction/concurrency/dispose)
- `EmbeddingComparerTests.cs` — 12 tests for multi-model comparison (pairwise similarities, metadata)
- `MiddlewareTests.cs` — 12 tests for OpenTelemetry and Retry middleware (decorators, extension methods)
- `BatchSizeAutoTunerTests.cs` — 11 tests for batch size tuning (auto mode, fixed mode, edge cases)

**Key testing patterns used:**
- Moq for mocking `IEmbeddingGenerator<string, Embedding<float>>`
- Deterministic seeded Random for reproducible embeddings
- Thread-safe progress reporting with lock guards + Task.Delay for async propagation
- CancellationToken verification with pre-cancelled tokens
- Concurrent access tests using Task.WhenAll
- Dispose/DisposeAsync propagation tests

**Challenges resolved:**
- Progress<T> is async by design - added small delays and thread-safe collections for reliable capture
- OnnxRuntimeException constructors are internal - used IOException for retry tests instead
- GetService<T> extension method mocking - used explicit type matching in mock setup

**Test coverage:**
- All new Wave 1 APIs have unit tests
- Empty input edge cases covered
- Cancellation token support verified
- Null parameter guards tested
- Thread safety validated for CachingDecorator

**Total test count:** 211 (67 new Wave 1 tests + 144 existing)
**Result:** All 211 tests passing on both net8.0 and net10.0

**Build validation:** `dotnet test tests/ElBruno.LocalEmbeddings.Tests/ --verbosity quiet` → 0 failures, 0 warnings


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

