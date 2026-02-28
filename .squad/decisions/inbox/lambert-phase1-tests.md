# Lambert: Phase 1 Unit Tests — Delivery Note

**By:** Lambert (Tester / QA)  
**Date:** 2025-07-16  
**Status:** Complete — 31 tests, all passing ✅

---

## What Was Done

Wrote unit tests for the Phase 1 security and performance fixes (SEC-001, SEC-006, PERF-01, PERF-02). All four implementations were already complete when the tests were written.

### Files Created

| File | Tests | Focus |
|------|-------|-------|
| `tests/ElBruno.LocalEmbeddings.Tests/ModelDownloaderSecurityTests.cs` | 11 | SEC-006 path traversal guard |
| `tests/ElBruno.LocalEmbeddings.Tests/HashVerificationTests.cs` | 12 | SEC-001 sidecar hash verification |
| `tests/ElBruno.LocalEmbeddings.Tests/MeanPoolingTests.cs` | 8 | PERF-02 SIMD correctness + PERF-01 regression |

### Minimal Production Code Changes

| File | Change | Reason |
|------|--------|--------|
| `src/ElBruno.LocalEmbeddings/OnnxEmbeddingModel.cs` | `ApplyMeanPooling`: `private` → `internal` | Enable direct unit testing of pooling algorithm |
| `src/ElBruno.LocalEmbeddings/ElBruno.LocalEmbeddings.csproj` | Added `InternalsVisibleTo` for test project | Required for above |
| `tests/ElBruno.LocalEmbeddings.Tests/ElBruno.LocalEmbeddings.Tests.csproj` | Added `Microsoft.ML.OnnxRuntime` reference | `DenseTensor<float>` needed in mean pooling tests |

---

## Key Finding: SEC-006 Guard Behavior

`DefaultPathHelper.SanitizeModelName` from the HuggingFace Downloader NuGet package replaces `/` with `_`. This means slash-based traversal names (`"../../escape"`) become safe subpaths (`".._.._ escape"`) that stay inside the cache — the `Path.GetFullPath` guard **never fires** for those inputs.

The guard fires for inputs without `/`, notably a bare `".."`:
- `Path.GetFullPath(Path.Combine(cacheDir, ".."))` resolves to the parent directory → outside cache → `ArgumentException` ✅

**Test design adjusted accordingly:**
- `".."` model name → verifies guard fires → `ArgumentException`
- Slash-based names → verify no files are created outside the cache (behavioral property test)
- Pure math tests document why the guard is necessary

---

## Test Results

```
Passed!  - Failed: 0, Passed: 89, Skipped: 0, Total: 89 (net8.0)
Passed!  - Failed: 0, Passed: 89, Skipped: 0, Total: 89 (net10.0)
```

All 89 non-integration tests pass (58 pre-existing + 31 new Phase 1 tests).

---

## Decisions Recommended

None — the implementations are already in place and correct. The test for the SEC-006 guard behavior (slash sanitization by the external package) is documented so future maintainers understand which inputs trigger the `ArgumentException` path versus the primary sanitization path.
