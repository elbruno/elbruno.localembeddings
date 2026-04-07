# Test Coverage Gap Analysis — Full Repository

**Author:** Lambert (Tester/QA)  
**Date:** 2026-02-28  
**Scope:** All 9 test projects, all source packages  
**Total tests discovered:** ~558 (across net8.0 + net10.0 TFMs, including Theory expansions)

---

## Executive Summary

The base library (`ElBruno.LocalEmbeddings.Tests`) has **strong coverage** — security, hashing, mean pooling, search, and DI are all well-tested. The Harrier test suite is the **weakest link**: it covers only validation/guard clauses with zero integration tests and zero tests of actual tokenization or embedding output. The SharedModelTests provide a good multilingual smoke test but use overly loose cross-lingual thresholds. Three NPU packages have **no source code and no tests** (empty scaffolds). The ImageEmbeddings.Downloader package also has no source or tests.

---

## 1. Per-Project Assessment

### 1.1 ElBruno.LocalEmbeddings.Tests — ⭐⭐⭐⭐ (Strong)

**Tests:** ~120+ test methods across 11 files  
**Coverage highlights:** Constructor guards, DI registration (4 overloads), hash verification (12 tests), path traversal security (11 tests), SIMD mean pooling (9 tests), FindClosest heap parity (9 unit + 3 integration), tokenizer (14 integration), embedding generator (28 integration), async patterns.

**What's missing:**

| Priority | Gap | Recommended Test |
|----------|-----|-----------------|
| P1 | `LocalEmbeddingGenerator.DisposeAsync()` — never tested | `DisposeAsync_ReleasesResources` |
| P1 | `LocalEmbeddingGenerator.CountTokens(string)` — no test | `CountTokens_ReturnsPositiveCount`, `CountTokens_EmptyString_ReturnsZero` |
| P1 | `Tokenizer.CountTokens(string, int?)` — no test | `CountTokens_KnownInput_ReturnsExpectedCount`, `CountTokens_WithMaxLength_TruncatesCount` |
| P1 | `CreateAsync(options, IProgress<double>?, ct)` — progress overload untested | `CreateAsync_WithProgress_ReportsProgress` |
| P2 | `OnnxEmbeddingModel.EmbeddingDimension` after successful load | `Load_ValidModel_SetsEmbeddingDimension` |
| P2 | `OnnxEmbeddingModel.IsLoaded` after successful load | `Load_ValidModel_IsLoadedTrue` |

### 1.2 ElBruno.LocalEmbeddings.Harrier.Tests — ⭐⭐ (Weak)

**Tests:** ~27 test methods across 5 files  
**Coverage:** Options defaults, downloader filename mapping (Theory), ONNX model error paths, tokenizer creation guards, generator creation preconditions.

**What's missing — CRITICAL GAPS:**

| Priority | Gap | Recommended Test |
|----------|-----|-----------------|
| **P0** | No test of actual tokenization output — token IDs, attention masks are completely untested | `Tokenize_KnownInput_ProducesExpectedTokenIds` [SkippableFact] |
| **P0** | No parsing of real `tokenizer.json` file — `HarrierTokenizer.Create` success path never exercised | `Create_WithRealTokenizerJson_Succeeds` [SkippableFact] |
| **P0** | No integration test for `HarrierEmbeddingGenerator.GenerateAsync` | `GenerateAsync_ProducesValidEmbeddings` [SkippableFact] |
| P1 | Instruction prefix behavior never tested end-to-end | `Tokenize_WithInstructionPrefix_PrependsPrefixToInput` |
| P1 | `TokenizeBatch` — zero tests | `TokenizeBatch_MultipleInputs_ProducesConsistentOutput` |
| P1 | `CountTokens` — zero tests | `CountTokens_ReturnsPositiveCount` |
| P1 | Successful `CreateAsync` — only error paths covered | `CreateAsync_WithRealModel_ReturnsGenerator` [SkippableFact] |
| P1 | DI extension methods (`AddHarrierEmbeddings` 3 overloads) — zero tests | `AddHarrierEmbeddings_Action_RegistersServices`, `AddHarrierEmbeddings_Configuration_BindsValues` |
| P1 | `Dispose()` / `DisposeAsync()` on generator — zero tests | `Dispose_AfterUse_DoesNotThrow`, `DisposeAsync_ReleasesResources` |
| P1 | `Metadata` property — zero tests | `Metadata_ReturnsCorrectModelId` |
| P1 | `GetService` — zero tests | `GetService_SelfType_ReturnsSelf` |
| P2 | `MaxLength` / `InstructionPrefix` properties after create | `Create_SetsMaxLengthFromTokenizerJson` |
| P2 | Model variant end-to-end (download + load correct variant) | `CreateAsync_WithFp16Variant_LoadsFp16Model` [SkippableFact] |

**Comparison to base library:** The base library's `LocalEmbeddingGeneratorTests.cs` has 28 integration tests exercising the full pipeline. Harrier has **zero integration tests**. This is the single biggest coverage gap in the entire repository.

### 1.3 ElBruno.LocalEmbeddings.SharedModelTests — ⭐⭐⭐ (Adequate)

**Tests:** 20 test methods, all SkippableFact (require local models)  
**Coverage:** 9 languages, 3 cross-lingual pairs, batch embeddings, determinism, normalization, empty input, dimension check.

**Gaps and recommendations:**

| Priority | Gap | Recommended Test |
|----------|-----|-----------------|
| P1 | Cross-lingual threshold `> 0.0` is too weak — any non-zero value passes | Raise to `> 0.3` or add specific known-pair assertions |
| P1 | Missing dissimilar tests for French, German, Japanese, Portuguese, Arabic, Korean | `French_DissimilarSentences_LowSimilarity`, etc. (6 tests) |
| P1 | Russian only in batch test — no dedicated similarity/dissimilarity tests | `Russian_SimilarSentences_HighSimilarity`, `Russian_DissimilarSentences_LowSimilarity` |
| P2 | Missing cross-lingual pairs: English↔German, English↔Japanese, Spanish↔Portuguese, Chinese↔Japanese | 4 additional cross-lingual tests |
| P2 | No test for very long input (truncation boundary behavior) | `LongInput_ProducesValidEmbedding_WithoutError` |
| P2 | No test for special characters / emoji / mixed scripts | `SpecialCharacters_ProducesValidEmbedding` |
| P2 | ModelFixture never disposes generators — ONNX resources leak until process exit | Add `IAsyncLifetime` or finalizer logging |

**Threshold assessment:** Same-language thresholds (0.5–0.6) are reasonable smoke thresholds. Cross-lingual `> 0.0` is meaningless — random vectors can have positive cosine similarity.

### 1.4 ElBruno.LocalEmbeddings.KernelMemory.Tests — ⭐⭐⭐ (Adequate)

**Tests:** 9 test methods in 1 file  
**Coverage:** Constructor null guard, embedding delegation, token counting heuristic + custom tokenizer, token splitting, dispose ownership (sync + async when ownsGenerator=true).

**Gaps:**

| Priority | Gap | Recommended Test |
|----------|-----|-----------------|
| P1 | `MaxTokens` property — never asserted | `Constructor_SetsMaxTokens_ToProvidedValue` |
| P1 | `GenerateEmbeddingAsync` with null text — no guard test | `GenerateEmbeddingAsync_NullText_ThrowsArgumentNullException` |
| P1 | All DI extension methods (6 overloads across 2 classes) — zero tests | `WithLocalEmbeddings_RegistersTextGenerator`, `AddLocalEmbeddingsWithKernelMemory_RegistersServices` |
| P1 | `DisposeAsync` when `ownsGenerator=false` — only true path tested | `DisposeAsync_WhenOwnsGeneratorFalse_DoesNotDispose` |
| P2 | `CountTokens` with actual `LocalEmbeddingGenerator` tokenizer branch | `CountTokens_WithLocalEmbeddingGenerator_UsesRealTokenizer` [SkippableFact] |

### 1.5 ElBruno.LocalEmbeddings.VectorData.Tests — ⭐⭐⭐⭐ (Strong)

**Tests:** 11 test methods across 2 files  
**Coverage:** DI registration (3 overloads), null/invalid guards, typed collection resolution, upsert/get/search lifecycle, empty search, concurrent access, missing vector annotation error.

**Gaps:**

| Priority | Gap | Recommended Test |
|----------|-----|-----------------|
| P1 | `InMemoryVectorStore.ListCollectionNamesAsync` — untested | `ListCollectionNamesAsync_ReturnsCreatedCollections` |
| P1 | `InMemoryVectorStore.CollectionExistsAsync` — untested | `CollectionExistsAsync_ReturnsTrue_AfterEnsure` |
| P1 | `InMemoryVectorStore.EnsureCollectionDeletedAsync` — untested | `EnsureCollectionDeletedAsync_RemovesCollection` |
| P2 | `GetDynamicCollection` — untested | `GetDynamicCollection_ReturnsWorkingCollection` |
| P2 | `GetService` — untested | `GetService_ReturnsExpectedService` |
| P2 | `DeleteAsync` on collection records | `DeleteAsync_RemovesRecord` |
| P2 | DI overloads taking `IConfiguration` | `AddLocalEmbeddingsWithInMemoryVectorStore_WithConfig_BindsValues` |

### 1.6 ElBruno.LocalEmbeddings.ImageEmbeddings.Tests — ⭐⭐⭐ (Adequate)

**Tests:** ~34 test methods across 7 files  
**Coverage:** Options defaults/path composition/validation (18 tests), encoder constructor guards (10 tests), search engine null guards (6 tests, 5 SkippableFact), tokenizer file size guard (2 tests), tokenizer encode length (1 test), DI null guards (2 tests).

**Gaps:**

| Priority | Gap | Recommended Test |
|----------|-----|-----------------|
| P1 | `ClipImageEncoder.Encode(string)` / `Encode(Stream)` — never tested with real model | `Encode_RealImage_ProducesValidVector` [SkippableFact] |
| P1 | `ClipTextEncoder.Encode(string)` — never tested with real model | `Encode_RealText_ProducesValidVector` [SkippableFact] |
| P1 | `ImageSearchEngine.IndexImages` / `AddImage` — never tested | `IndexImages_PopulatesImageCount` [SkippableFact] |
| P1 | `ImageSearchEngine.SearchByImage` — never tested | `SearchByImage_ReturnsRankedResults` [SkippableFact] |
| P1 | `ClipImageEncoder.Dispose` / `ClipTextEncoder.Dispose` — never tested | `Dispose_IsIdempotent` |
| P1 | Successful `AddImageEmbeddings` registration (only null guards tested) | `AddImageEmbeddings_WithValidConfig_RegistersServices` |
| P2 | `SearchByText` ranking behavior (not just empty-index) | `SearchByText_MultipleImages_ReturnsRelevantFirst` [SkippableFact] |

### 1.7 NPU Projects — ❌ (No Coverage)

**ElBruno.LocalEmbeddings.Npu**, **ElBruno.LocalEmbeddings.Npu.Intel**, **ElBruno.LocalEmbeddings.Npu.Qualcomm**: All three source projects and their test projects are **empty scaffolds** with no .cs files. No action needed until source code is added.

### 1.8 ElBruno.LocalEmbeddings.ImageEmbeddings.Downloader — ❌ (No Coverage)

Empty scaffold with no .cs files. No test project exists. No action needed until source code is added.

---

## 2. Cross-Cutting Testing Gaps

These gaps affect the entire repository and are not specific to any one project.

| Priority | Category | Gap | Recommendation |
|----------|----------|-----|----------------|
| **P0** | **Concurrency** | No test of multiple simultaneous `GenerateAsync` calls on the same generator instance | Add `GenerateAsync_ConcurrentCalls_AllReturnValidResults` in both base and Harrier test projects — ONNX Runtime sessions are thread-safe, but this should be proven |
| P1 | **Cancellation** | CancellationToken tested only in `ModelDownloaderTests.EnsureModelAsync_WhenCancelled_ThrowsOperationCanceledException`. No cancellation tests for `GenerateAsync`, `TokenizeBatch`, or any Harrier method | Add cancellation tests for generator and tokenizer batch operations |
| P1 | **Large batch** | No test with >100 inputs in a single `GenerateAsync` call — batch splitting / memory behavior untested | `GenerateAsync_LargeBatch_500Items_CompletesSuccessfully` |
| P1 | **Disposal** | `DisposeAsync` tested only in KernelMemory adapter. `LocalEmbeddingGenerator.DisposeAsync()` and `HarrierEmbeddingGenerator.DisposeAsync()` have no tests | Add async disposal tests for all generator types |
| P2 | **Memory pressure** | No test generating many embeddings in sequence (e.g., 1000 iterations) to detect leaks | `GenerateAsync_RepeatedCalls_NoMemoryGrowth` (monitor working set) |
| P2 | **Timeout** | No tests verifying behavior under slow conditions or very long inputs that approach token limits | `GenerateAsync_MaxLengthInput_CompletesWithinTimeout` |

---

## 3. Testing Infrastructure Assessment

### ModelFixture (SharedModelTests)
- **Strengths:** Thread-safe `Lazy<>` singletons, environment variable overrides for CI, clean skip-if-unavailable pattern.
- **Weaknesses:** No disposal of ONNX resources; Harrier creation blocks on `.GetAwaiter().GetResult()` inside Lazy; no retry if model loading fails.
- **Recommendation (P2):** Implement `IAsyncLifetime` on a collection fixture to dispose generators at end of test run.

### Shared Test Utilities
- **Current state:** No shared test helpers project. Each project independently creates mock generators, builds embedding arrays, etc.
- **Recommendation (P2):** Create a `tests/ElBruno.LocalEmbeddings.TestUtilities/` project with:
  - `EmbeddingFactory` — helper to create `Embedding<float>` from known vectors
  - `MockEmbeddingGenerator` — reusable mock implementing `IEmbeddingGenerator<string, Embedding<float>>`
  - `TestModelPaths` — centralized model path resolution with env var fallback
  - This would reduce duplication across 6+ test projects.

### Dependency Consistency
- Test projects use consistent patterns: xUnit, Moq, Xunit.SkippableFact.
- Base library tests correctly have `InternalsVisibleTo` for testing internal types.
- Harrier tests also use `InternalsVisibleTo` for `ExtractEmbeddings` and `GetOnnxFileName`.
- **No issues found** with dependency consistency.

---

## 4. Quality Assessment Summary

### Edge Cases
- **Null/empty inputs:** Well covered in base and ImageEmbeddings projects. Harrier covers null/empty for `Create` but not for `Tokenize`/`GenerateAsync`.
- **Boundary values:** `maxLength` boundaries tested in base tokenizer and OnnxEmbeddingModel. Not tested in Harrier.
- **Error paths:** Thoroughly tested in base library (12 hash tests, 11 security tests). Harrier only tests guard clauses.

### Table-Driven Tests
- Good use of `[Theory]/[InlineData]` in: `ModelDownloaderSecurityTests`, `FindClosestTests`, `EmbeddingGeneratorFindClosestTests`, `ImageEmbeddingsOptionsValidationTests`, `ClipEncoderConstructorTests`, `HarrierModelDownloaderTests`.
- **Missing Theory usage:** `KernelMemory.CountTokens` should be Theory with multiple inputs. `SharedModelTests` similarity tests repeat the same pattern for each language — could be consolidated into a Theory.

### SkippableFact Usage
- Correctly used for integration tests requiring model files (base library, ImageEmbeddings, SharedModelTests).
- **Missing:** Harrier has zero SkippableFacts — all integration testing is absent.

---

## 5. Priority Summary

### P0 — Critical (blocks confidence in shipping)
1. **Harrier has zero integration tests** — no proof that tokenization, embedding generation, or instruction prefix actually work
2. **No concurrency tests** for any generator — thread safety is claimed but unproven
3. **Harrier tokenizer.json parsing** completely untested with real files

### P1 — Important (should be addressed before next release)
4. Cross-lingual similarity thresholds in SharedModelTests are meaninglessly loose (`> 0.0`)
5. `CountTokens` untested in both base and Harrier libraries
6. `DisposeAsync` untested for all generator types
7. DI extension methods untested in Harrier and KernelMemory
8. CancellationToken propagation untested in generators
9. Large batch behavior untested
10. Missing dissimilar-sentence tests for 6 languages in SharedModelTests
11. ImageEmbeddings encoder `Encode` methods untested with real models
12. ImageSearchEngine `IndexImages`/`SearchByImage` untested

### P2 — Nice to Have
13. Memory pressure / leak detection tests
14. Shared test utilities project
15. ModelFixture disposal
16. Additional cross-lingual language pairs
17. Special character / emoji embedding tests
18. InMemoryVectorStore CRUD completeness (Delete, ListNames, Exists)

---

## Recommended Test Count by Project

| Project | Current Tests | Recommended New Tests | Priority Breakdown |
|---------|--------------|----------------------|-------------------|
| Harrier.Tests | 27 | 18+ | 3 P0, 10 P1, 5 P2 |
| SharedModelTests | 20 | 12+ | 0 P0, 8 P1, 4 P2 |
| LocalEmbeddings.Tests | 120+ | 8+ | 0 P0, 6 P1, 2 P2 |
| ImageEmbeddings.Tests | 34 | 8+ | 0 P0, 6 P1, 2 P2 |
| KernelMemory.Tests | 9 | 6+ | 0 P0, 5 P1, 1 P2 |
| VectorData.Tests | 11 | 7+ | 0 P0, 3 P1, 4 P2 |
| **Cross-cutting** | — | 4+ | 1 P0, 3 P1 |
| **Total** | ~221 unique | **~63 new tests** | **4 P0, 41 P1, 18 P2** |

---

*Lambert — "If the Harrier can't prove its tokenizer works, it doesn't ship."*
