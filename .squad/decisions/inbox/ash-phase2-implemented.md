# Security Decision Note — Phase 2 Input Validation Fixes Implemented

**By:** Ash (Security Engineer)  
**Date:** 2026-06-XX  
**Status:** Implemented — `dotnet build` passes, 0 errors, 0 warnings

---

## Summary

Three input validation security fixes (SEC-003, SEC-004, SEC-005) have been applied to
`ElBruno.LocalEmbeddings.ImageEmbeddings`. All changes are surgical, backward-compatible,
and consistent with the project's coding conventions.

---

## SEC-003 — Path traversal prevention in `ImageEmbeddingsOptions`

**File:** `src/ElBruno.LocalEmbeddings.ImageEmbeddings/Options/ImageEmbeddingsOptions.cs`

**Change:** Converted the four file-name properties (`TextModelFileName`, `VisionModelFileName`,
`VocabFileName`, `MergesFileName`) from auto-properties to full properties backed by private fields.
A shared `ValidateFileName` static helper enforces three rules on each setter:
1. Value must not be null or whitespace (`ArgumentException.ThrowIfNullOrWhiteSpace`).
2. Value must not contain `..` (path traversal sequences).
3. Value must not contain characters returned by `Path.GetInvalidFileNameChars()`.

**Impact:** Callers using the default values are unaffected. Any attempt to set a property to
a traversal path like `"../../../etc/passwd"` will immediately throw `ArgumentException`.

---

## SEC-004 — Null/existence guards in CLIP encoder constructors

**Files:**
- `src/ElBruno.LocalEmbeddings.ImageEmbeddings/ClipImageEncoder.cs`
- `src/ElBruno.LocalEmbeddings.ImageEmbeddings/ClipTextEncoder.cs`

**Changes:**
- `ClipImageEncoder(string modelPath)`: Guards added before `new InferenceSession(...)`:
  - `ArgumentException.ThrowIfNullOrWhiteSpace(modelPath)`
  - `File.Exists(modelPath)` → throws `FileNotFoundException` with descriptive message if absent.
- `ClipTextEncoder(string modelPath, string vocabPath, string mergesPath)`: Same pattern for all three paths before ONNX session and tokenizer initialization.

**Rationale:** Failing fast at the constructor boundary surfaces misconfiguration clearly instead
of letting ONNX Runtime throw a cryptic native exception.

---

## SEC-005 — Null guards in `ImageSearchEngine`

**File:** `src/ElBruno.LocalEmbeddings.ImageEmbeddings/ImageSearchEngine.cs`

**Changes:**
- Constructor: `ArgumentNullException.ThrowIfNull` for both `imageEncoder` and `textEncoder`.
- `SearchByText(string query, ...)`: `ArgumentException.ThrowIfNullOrWhiteSpace(query)` added.
- `SearchByImage(string imagePath, ...)`: `ArgumentException.ThrowIfNullOrWhiteSpace(imagePath)` added.

**Note:** `IndexImages` and `AddImage` already contained their own existence checks
(`Directory.Exists` / `File.Exists`); no changes were needed there.

---

## Recommendation for the team

These fixes complete Phase 2 of the security audit. Remaining open items from the Phase 1 audit:
- **SEC-002** (static `HttpClient` / `ModelDownloader` DI bypass) — medium priority.
- **SEC-007/008/009** (sync-over-async, OnnxRuntime version, ClipTokenizer file size limit) — low priority.
