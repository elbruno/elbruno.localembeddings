# Lambert: Phase 3 Performance Tests

**By:** Lambert (Tester)  
**Date:** 2025-07-17  
**Status:** Tests written and passing

## Summary

Wrote unit and integration tests for the Phase 3 performance fixes (PERF-09, PERF-08, PERF-12/13) being implemented by Parker.

## New Test File

**`tests/ElBruno.LocalEmbeddings.Tests/FindClosestTests.cs`** — 12 tests total (9 unit, 3 integration).

## PERF-09 — Heap-based `FindClosest` (8 unit tests)

Tests are in the `FindClosestTests` class and verify correctness of the PriorityQueue-based implementation against a LINQ reference:

| Test | Purpose |
|------|---------|
| `FindClosest_ReturnsTopKResults_ByScore` | Results are sorted descending by similarity score |
| `FindClosest_TopKGreaterThanCorpus_ReturnsAll` | topK > corpus.Count → all items returned (Theory: 10, 100, 1000) |
| `FindClosest_TopKOne_ReturnsHighestScore` | topK=1 edge case: only the max-score item returned |
| `FindClosest_TopKEqualsCorpus_MatchesOrderByDescending` | **Parity test** — 100-item deterministic corpus, heap vs LINQ, index-and-score exact match |
| `FindClosest_EmptyCorpus_ReturnsEmpty` | Empty corpus returns empty list |
| `FindClosest_AllEqualScores_ReturnsTopK` | All identical vectors → exactly topK items, all distinct indices |
| `FindClosest_LargeCorpus_TopKSubset_MatchesLinqReference` | **Parity test** — 200-item corpus, topK=10, heap vs LINQ |
| `FindClosest_WithMinScore_HeapOnlyIncludesAboveThreshold` | minScore filter still applied correctly in heap path |

**Parity test design:** Use `new Random(42)` (fixed seed) to generate a deterministic corpus; call `FindClosest` (heap-based after Parker's changes); compare each `(Index, Score)` pair against an inline LINQ `.OrderByDescending().ThenBy().Take()` reference. Tolerances: 5 decimal places for float comparison.

## PERF-08/12/13 — Tokenizer Regression (3 integration tests)

| Test | Covers |
|------|--------|
| `Tokenize_KnownInput_ProducesExpectedSpecialTokenLayout` | CLS at [0], correct lengths, zero-mask for padding (PERF-08) |
| `Tokenize_SameInputTwice_ProducesBitwiseIdenticalOutput` | No shared mutable state in optimized path (PERF-08) |
| `TokenizeBatch_OutputMatchesSingleTokenizeCalls_AfterToListRemoval` | Batch rows match per-item Tokenize (PERF-12/13) |

All use `[SkippableFact]` + `[Trait("Category", "Integration")]` and skip cleanly when vocab model is unavailable, consistent with the existing `TokenizerTests.cs` pattern.

## Verification

```
dotnet build                     → Build succeeded. 0 Warning(s), 0 Error(s)
dotnet test --filter "Category!=Integration"
                                 → Passed: 99, Failed: 0 (net8.0 and net10.0)
```
