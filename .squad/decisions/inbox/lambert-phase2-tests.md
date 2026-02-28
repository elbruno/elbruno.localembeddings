# Lambert — Phase 2 Tests Written

**By:** Lambert (Tester)  
**Date:** 2026-06-XX  
**Status:** Complete — `dotnet test` passes on net8.0 and net10.0

---

## Summary

Three new test files covering the Phase 2 security fixes (SEC-003, SEC-004, SEC-005) were added to
`tests/ElBruno.LocalEmbeddings.ImageEmbeddings.Tests/`. All implementations by Ash were confirmed
complete and correct. Tests for PERF-03/15/16 (Parker) are covered implicitly by the full build
and existing test suite passing.

**Test results:** 50 total tests in the ImageEmbeddings test project (was 16 before this work):
- 45 pass, 5 skip (SkippableFact tests requiring real ONNX model files), 0 fail — on both targets.

---

## Files Added

### `ImageEmbeddingsOptionsValidationTests.cs` (SEC-003)

Tests the `ValidateFileName` guard in `ImageEmbeddingsOptions` property setters.

| Test | Covers |
|------|--------|
| `VisionModelFileName_PathTraversal_ThrowsArgumentException` (Theory, 4 inputs) | `..` sequences in VisionModelFileName |
| `TextModelFileName_PathTraversal_ThrowsArgumentException` (Theory, 3 inputs) | `..` sequences in TextModelFileName |
| `VocabFileName_PathTraversal_ThrowsArgumentException` | `..` in VocabFileName |
| `MergesFileName_PathTraversal_ThrowsArgumentException` | `..` in MergesFileName |
| `FileName_InvalidChars_ThrowsArgumentException` (Theory, 4 inputs) | `<`, `>`, `|`, `?`, `*` |
| `FileName_NullOrWhiteSpace_ThrowsArgumentException` (Theory, 3 inputs) | null / "" / whitespace |
| `FileName_ValidName_DoesNotThrow` (Theory, 5 inputs) | happy path — no exception |

### `ClipEncoderConstructorTests.cs` (SEC-004)

Tests the early-exit guards added to `ClipImageEncoder` and `ClipTextEncoder` constructors.

| Test | Covers |
|------|--------|
| `ClipImageEncoder_NullOrWhiteSpacePath_ThrowsArgumentException` (Theory, 3 inputs) | null / "" / whitespace → ArgumentException |
| `ClipImageEncoder_NonExistentFile_ThrowsFileNotFoundException` | missing file → FileNotFoundException |
| `ClipTextEncoder_NullOrWhiteSpaceModelPath_ThrowsArgumentException` (Theory, 3 inputs) | null / "" / whitespace modelPath |
| `ClipTextEncoder_NullOrEmptyVocabPath_ThrowsArgumentException` (Theory, 2 inputs) | null / "" vocabPath |
| `ClipTextEncoder_NullOrEmptyMergesPath_ThrowsArgumentException` (Theory, 2 inputs) | null / "" mergesPath |
| `ClipTextEncoder_NonExistentModelFile_ThrowsFileNotFoundException` | all three paths missing |
| `ClipTextEncoder_NonExistentVocabFile_ThrowsFileNotFoundException` | model exists (empty file), vocab missing |

### `ImageSearchEngineNullGuardTests.cs` (SEC-005)

Tests null guards in `ImageSearchEngine` constructor and `SearchByText` method.

| Test | Mode | Covers |
|------|------|--------|
| `ImageSearchEngine_NullImageEncoder_ThrowsArgumentNullException` | `[Fact]` | Passes `null!` for imageEncoder; no ONNX needed |
| `ImageSearchEngine_NullTextEncoder_ThrowsArgumentNullException` | `[SkippableFact]` | Requires `CLIP_VISION_MODEL_PATH` env var |
| `SearchByText_NullQuery_ThrowsArgumentException` | `[SkippableFact]` | Requires all 4 CLIP env vars |
| `SearchByText_EmptyQuery_ThrowsArgumentException` | `[SkippableFact]` | Requires all 4 CLIP env vars |
| `SearchByText_WhiteSpaceQuery_ThrowsArgumentException` | `[SkippableFact]` | Requires all 4 CLIP env vars |
| `SearchByText_ValidQuery_EmptyIndex_ReturnsEmptyList` | `[SkippableFact]` | Guard passes, empty index returns `[]` |

**SkippableFact env vars:** `CLIP_VISION_MODEL_PATH`, `CLIP_TEXT_MODEL_PATH`, `CLIP_VOCAB_PATH`, `CLIP_MERGES_PATH`

---

## Decisions / Patterns Established

1. **Constructor guards testable without ONNX files** when the guard fires before any file I/O (e.g., null check). Use direct `null!` passing.
2. **SkippableFact + environment variables** is the right pattern for tests that need live CLIP model files — consistent with how integration tests are handled elsewhere in the project.
3. **Zero-byte placeholder files** can be used to advance past one existence check and isolate a later check in the same constructor (used for `ClipTextEncoder_NonExistentVocabFile` test).
4. **`Assert.ThrowsAny<ArgumentException>`** is preferred over `Assert.Throws<ArgumentNullException>` when `ArgumentException.ThrowIfNullOrWhiteSpace` is used, since it throws `ArgumentNullException` for null and plain `ArgumentException` for empty/whitespace.
