# Test Coverage Improvements — Implementation Complete

**Author:** Lambert (Tester/QA)  
**Date:** 2026-02-28  
**Status:** Implemented

---

## Summary

Implemented all 6 test coverage improvements identified in the gap analysis. Total of ~33 new tests added across 5 test projects. All build with 0 errors, 0 warnings on both net8.0 and net10.0.

## Changes by Item

### 1. Harrier Integration Tests (P0) — NEW FILE

**File:** `tests/ElBruno.LocalEmbeddings.Harrier.Tests/HarrierIntegrationTests.cs`

- `CreateAsync_WithRealModel_ReturnsGenerator` — SkippableFact
- `GenerateAsync_ProducesValidEmbeddings` — verifies 640 dimensions
- `GenerateAsync_DeterministicOutput` — same input = same output
- `Tokenize_KnownInput_ProducesValidTokenIds` — verifies BOS/EOS tokens

### 2. Harrier Unit Test Gaps (P1) — UPDATED + NEW FILE

**Updated:** `HarrierTokenizerTests.cs` — 3 new maxLength boundary tests (1, 2, <3)  
**Updated:** `HarrierEmbeddingGeneratorTests.cs` — 2 idempotent disposal tests  
**New:** `HarrierDIExtensionsTests.cs` — 6 tests for all AddHarrierEmbeddings overloads

### 3. SharedModelTests Improvements (P1) — UPDATED

**Updated:** `MultilingualEmbeddingTests.cs`

- Cross-lingual threshold raised from `> 0.0` to `> 0.3`
- 6 new dissimilar-sentence tests (French, German, Japanese, Portuguese, Arabic, Korean)
- 2 new Russian dedicated tests (similar + dissimilar)

### 4. Cross-Cutting Test Gaps (P1) — NEW FILES

**New:** `ConcurrencyTests.cs` — 10 concurrent GenerateAsync calls  
**New:** `CancellationTests.cs` — pre-cancelled token propagation  
**New:** `DisposalTests.cs` — DisposeAsync + post-dispose ObjectDisposedException

### 5. Base Library Test Gaps (P1) — NEW FILE

**New:** `CountTokensTests.cs` — CountTokens positive count + empty string

### 6. KernelMemory + VectorData Gaps (P1) — UPDATED

**Updated:** `LocalEmbeddingTextGeneratorTests.cs` — DisposeAsync with ownsGenerator=false  
**Updated:** `InMemoryVectorStoreTests.cs` — ListCollectionNames, CollectionExists, EnsureCollectionDeleted

## Infrastructure Changes

- `ElBruno.LocalEmbeddings.Harrier.csproj`: OnnxRuntime 1.24.2 → 1.24.4 (resolved NU1605)
- `ElBruno.LocalEmbeddings.Harrier.Tests.csproj`: Added DI, Configuration, Options package references for DI extension tests

## Compatibility Notes

- maxLength boundary tests use `maxLength=3` as new minimum (compatible with Dallas's upcoming change)
- All SkippableFact tests skip cleanly when model files are unavailable
- Tests work on both net8.0 and net10.0

---

*Lambert — "33 new tests. Zero failures. That's how we ship."*
