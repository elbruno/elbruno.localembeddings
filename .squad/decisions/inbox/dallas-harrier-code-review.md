# Harrier Package — Deep Code Quality Review

**By:** Dallas (Core Dev)  
**Date:** 2026-02-28  
**Scope:** `src/ElBruno.LocalEmbeddings.Harrier/` — full implementation review against base library patterns

---

## 1. HarrierOnnxEmbeddingModel.cs

### ✅ Good

- **ArrayPool usage (lines 186–218):** Buffers are rented before the `try` and always returned in the `finally`. Correct pattern matching the base library (PERF-01).
- **Tensor construction (lines 197–198):** `DenseTensor<long>` sliced to exact size via `.AsMemory(0, totalSize)`. Prevents rented-array overflow into ONNX Runtime.
- **No token_type_ids (line 200–205):** Correct — Harrier is Gemma-based, not BERT. The base library conditionally adds `token_type_ids` only when the model expects it; Harrier correctly skips it entirely since the model never has this input.
- **Direct sentence_embedding output (line 211):** No mean pooling needed — pooling and L2 normalization are baked into the Harrier ONNX graph. Reading the first output directly is correct.
- **ExtractEmbeddings (lines 224–240):** Uses `DenseTensor<float>.Buffer.Span` for contiguous flat access, then `Slice/ToArray`. Efficient — matches the SIMD-friendly pattern established in the base library.
- **Disposal (lines 243–250):** Idempotent, disposes `InferenceSession`, nulls reference. Matches base pattern.
- **Validation (lines 54–72, 113–127, 148–183):** Thorough — disposed check, null checks, length mismatch checks, sequence length consistency. All match or exceed base library patterns.
- **Thread count validation (lines 94–100):** Same `ValidateThreadCount` helper as base library.

### ⚠️ Improvement Needed

- **Missing Linux ONNX Runtime alias workaround (compare base lines 106–141):** The base `OnnxEmbeddingModel.Load()` calls `EnsureLinuxOnnxRuntimeAliases()` to handle Linux native library resolution issues. Harrier's `Load()` (line 48) does not. If Harrier is used on Linux, it may fail with `DllNotFoundException` for the same reasons the base library needed this fix.
  - **Recommendation:** Call the same (or equivalent) alias-creation logic. Since the base library's method is `private static`, consider extracting it to a shared utility in the base library and calling it from both.

- **Missing DllNotFoundException error handling (compare base lines 80–96):** The base library wraps `InferenceSession` construction in a `catch` for `DllNotFoundException`/`TypeInitializationException` and provides a detailed diagnostic error message including OS, architecture, and native library paths. Harrier's `Load()` (line 85) creates the session without this protection.
  - **Recommendation:** Add the same try/catch pattern. This is especially important since Harrier models are large and users are more likely to encounter platform-specific issues.

- **`_outputNames` captured but not strictly necessary (line 87):** The Harrier model always outputs `sentence_embedding`. The base library stores `_outputNames` for the same reason, so this is fine structurally, but since Harrier always has exactly one output, a hardcoded name would be more explicit and less fragile.
  - **Recommendation:** Low priority. Current approach is flexible and correct.

- **EmbeddingDimension from `First()` (line 90–91):** Uses `.Values.First()` without verifying the output is actually `sentence_embedding`. If the ONNX model has multiple outputs, this might read the wrong one. The base library has the same pattern, so this is consistent, but for Harrier it would be safer to look up `"sentence_embedding"` by name.
  - **Recommendation:** Consider `_session.OutputMetadata["sentence_embedding"]` instead of `.First()`.

### ❌ Bug/Issue

- **No bugs found.** The implementation is solid.

---

## 2. HarrierTokenizer.cs

### ✅ Good

- **Factory pattern (line 59):** `Create()` static method follows the team's pattern for construction. Private constructor prevents invalid instances.
- **Path resolution (lines 66–68):** Accepts directory or file path, auto-appends `tokenizer.json`. Matches base `Tokenizer` pattern for `vocab.txt`.
- **Input validation (lines 61–77):** Null/empty path, missing file, non-positive maxLength all checked.
- **BOS/EOS handling (lines 119–137):** BOS at position 0, content tokens from position 1, EOS after content. Correct sequence for Harrier's Gemma tokenizer.
- **Instruction prefix prepending (lines 101–104):** Prepends `_instructionPrefix` to text before tokenization when set. This is the correct location — prefix becomes part of the tokenized input, affecting the embedding.
- **Batch tokenization (lines 153–178):** Uses `as IList<string> ?? .ToList()` pattern matching PERF-12/13. CancellationToken honored per item.
- **Thread safety:** `BpeTokenizer` is immutable after construction. No mutable state in `HarrierTokenizer` fields. Thread-safe.
- **tokenizer.json parsing (lines 198–268):** Robust handling of both merge formats (array-of-arrays and string format). `JsonDocumentOptions` allows trailing commas and comments for resilience.

### ⚠️ Improvement Needed

- **No file size guard for tokenizer.json (compare SEC-009):** The base library's CLIP tokenizer has a 50 MB size guard before reading vocab files. `LoadFromTokenizerJson` (line 200) reads the entire tokenizer.json into memory without size limits. Harrier tokenizer.json files can be 10+ MB.
  - **Recommendation:** Add a size guard (e.g., `const long MaxTokenizerFileSizeBytes = 100 * 1024 * 1024`) before `File.OpenRead`.

- **Merges parsing: partial array not validated (lines 241–251):** When merges are in array-of-arrays format `[["a", "b"]]`, the code reads up to 2 elements. If a merge entry has only 1 element, `parts[1]` remains `null` (default for `string[]`), and `writer.Write(null)` writes nothing. This silently produces malformed merge text like `"a "` (token + space + newline). This won't crash but produces a wrong merge entry.
  - **Recommendation:** Validate `idx == 2` after the inner loop and skip or throw for malformed entries.

- **CountTokens allocates full arrays (lines 183–193):** `CountTokens` calls `Tokenize`, which allocates `long[maxLength]` × 2 arrays (potentially `long[8192]` × 2 = 128 KB), just to count the `1`s in the attention mask. For a 8192 max length, this is wasteful for a simple token count.
  - **Recommendation:** Add a lightweight count method that calls `_tokenizer.EncodeToIds()` directly and returns `encoding.Count + 2` (for BOS/EOS). Low priority since CountTokens is not a hot path.

### ❌ Bug/Issue

- **Index out of bounds when maxLength = 1 (lines 107–137):** When `effectiveMaxLength = 1`:
  1. `contentMaxLength = 1 - 2 = -1` → clamped to `1` (line 109)
  2. `inputIds = new long[1]` (line 116)
  3. BOS set at `inputIds[0]` (line 120) — OK
  4. `encoding` can have up to 1 token. If text is non-empty, `copyLength = 1`
  5. Loop: `inputIds[0 + 1]` = `inputIds[1]` → **IndexOutOfRangeException** on a length-1 array

  While `maxLength=1` is pathological, the validation allows it (`maxLength > 0`). The base library avoids this because `BertTokenizer.EncodeToIds` handles special tokens internally.
  - **Fix:** After clamping, ensure `contentMaxLength = Math.Min(contentMaxLength, effectiveMaxLength - 1)` to reserve the BOS slot. Or raise the minimum maxLength to 3 (BOS + at least 1 token + EOS).

- **SentencePiece normalizer risk with BpeTokenizer:** Harrier uses a Gemma 3 tokenizer with SentencePiece conventions where spaces are represented as `▁` (U+2581). The `BpeTokenizer.Create(vocabStream, mergesStream)` method creates a standard BPE tokenizer from vocab and merges — it does **not** automatically apply SentencePiece pre-tokenization normalization (space → `▁`). If the tokenizer.json has a `normalizer` section that maps spaces to `▁`, this normalization is being silently **skipped**.
  - **Impact:** Tokens produced may differ from the original Harrier tokenizer, potentially producing incorrect embeddings. The severity depends on whether the Harrier ONNX model compensates or whether the BPE vocab entries already handle space representations.
  - **Recommendation:** HIGH PRIORITY. Verify against the actual Harrier tokenizer.json whether a normalizer section exists and what it does. If space→▁ normalization is present, implement it as a pre-processing step before calling `_tokenizer.EncodeToIds`. Test by comparing token IDs against the Python `tokenizers` library output.

---

## 3. HarrierModelDownloader.cs

### ✅ Good

- **Path traversal protection (lines 63–69):** `DefaultPathHelper.SanitizeModelName` + `Path.GetFullPath` + `StartsWith` check. Matches SEC-006 pattern exactly.
- **SHA-256 sidecar writing (lines 143–145):** Hash written after successful download. Matches SEC-001 pattern.
- **Expected hash verification (lines 148–155):** When `ExpectedHash` is set, downloaded file is verified. Correct `StringComparison.OrdinalIgnoreCase`.
- **Variant support (lines 164–171):** Clean `switch` expression for model file names. Includes fallback in `ResolveModelPath` (lines 176–194).
- **`.onnx_data` companion handling (line 74):** Correctly constructs data file name as `{model}.onnx_data`. Included in required files for download.
- **Required vs optional files (lines 89–94):** ONNX model files are required; tokenizer files are optional. Correct for initial setup where tokenizer files might be pre-existing.
- **Post-download validation (lines 129–141):** Verifies both ONNX model and tokenizer.json exist. Descriptive error messages.

### ⚠️ Improvement Needed

- **No concurrent download serialization (compare base ModelDownloader lines 38, 100–109):** The base `ModelDownloader` uses `ConcurrentDictionary<string, SemaphoreSlim>` to serialize concurrent downloads for the same model. `HarrierModelDownloader.EnsureModelAsync` has no such protection. If multiple threads/services call `EnsureModelAsync` concurrently for the same model, they'll race on file downloads and moves, potentially corrupting files or causing I/O conflicts.
  - **Recommendation:** Add the same `_downloadLocks` pattern. This is important for DI scenarios where multiple singleton services might resolve simultaneously.

- **No sidecar hash verification on cache hit (lines 78–85):** When the model already exists, the base library verifies the sidecar hash and re-downloads on mismatch (SEC-001). Harrier only checks `File.Exists`. A corrupted cached file would be used without detection.
  - **Recommendation:** Add `SidecarHashValid()` check before returning the cached path. Delete and re-download if invalid.

- **File move logic moves ALL files (line 119 vs base line 189):** Base library uses `Directory.GetFiles(onnxSubDir, "*.onnx")` — only moves ONNX files. Harrier uses `Directory.GetFiles(onnxSubDir)` — moves everything from the `onnx/` subdirectory. This is actually intentional (to also move `.onnx_data` files), but it's overly broad and could move unexpected files.
  - **Recommendation:** Use a more specific pattern like `"model*"` or explicitly move the known files (onnxFileName and onnxDataFileName).

- **`onnx/` subdirectory not cleaned up (line 116–127):** After moving files, the empty `onnx/` directory remains. Not harmful but untidy.
  - **Recommendation:** `Directory.Delete(onnxSubDir, false)` after the move loop if the directory is empty.

- **HttpClient created without disposal tracking (line 49):** The parameterless constructor creates `new HttpClient(new SocketsHttpHandler {...})` but the `HttpClient` is not disposed when the downloader is disposed (class isn't `IDisposable`). Since the constructor is the owner, it should manage the lifetime.
  - **Recommendation:** Either make `HarrierModelDownloader` implement `IDisposable` and track ownership, or document that the caller-provided `HttpClient` overload is preferred.

### ❌ Bug/Issue

- **Cache hit skips `.onnx_data` verification (lines 78–85):** The cache check only verifies the `.onnx` model file and `tokenizer.json` exist. The `.onnx_data` companion file (external weights) is not checked. If the `.onnx_data` file is missing or corrupted, the ONNX model will fail at runtime with a confusing error.
  - **Recommendation:** Also check `File.Exists` for the `.onnx_data` file in the cache hit path.

---

## 4. HarrierEmbeddingGenerator.cs

### ✅ Good

- **Async factory pattern (lines 93–102):** `CreateAsync` downloads asynchronously, then constructs synchronously. Matches base `LocalEmbeddingGenerator.CreateAsync` pattern.
- **Overload chain (lines 67–84):** Three CreateAsync overloads (default, with options, with options+progress) chain cleanly.
- **Thread safety after construction (lines 27–30):** `_model`, `_tokenizer`, `_metadata` are all `readonly`. No mutable shared state except `_disposed` (standard pattern).
- **Disposal (lines 170–183):** `Dispose()` disposes `_model` (InferenceSession). `DisposeAsync` delegates to `Dispose` + `ValueTask.CompletedTask`. Matches base pattern.
- **GenerateAsync (lines 105–132):** Identical flow to base: materialize IEnumerable, empty check, tokenize batch, generate batch, wrap in M.E.AI types. Returns `Task.FromResult` (synchronous compute, no true async).
- **GetService (lines 135–156):** Both generic and non-generic overloads match base library exactly.
- **CountTokens (lines 163–167):** Disposed check before delegating. Consistent with base.
- **IList materialization (line 113):** `values as IList<string> ?? values.ToList()` — PERF-12/13 pattern.

### ⚠️ Improvement Needed

- **`SharedModelDownloadHttpClient` is bare `new HttpClient()` (line 26):** No `SocketsHttpHandler` with `PooledConnectionLifetime`. The base library has the same issue (line 24 of `LocalEmbeddingGenerator.cs`), so this is a pre-existing pattern gap (SEC-002 only fixed the `ModelDownloader` parameterless constructor). However, since this is new code, it should follow the corrected pattern from the start.
  - **Recommendation:** `new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) })`.

- **No synchronous constructor (compare base lines 36–63):** The base `LocalEmbeddingGenerator` has a synchronous constructor for backward compatibility. Harrier only has async `CreateAsync`. This is actually BETTER design (avoids SEC-007 sync-over-async risk), but the DI `AddHarrierEmbeddingsCore` (ServiceCollectionExtensions line 107) calls `.GetAwaiter().GetResult()` anyway, reintroducing the deadlock risk.
  - **Recommendation:** Document the deadlock risk in `AddHarrierEmbeddingsCore` (it already has a comment, but the `<remarks>` on the public method could be stronger).

- **Metadata providerName (line 53):** Uses `"LocalEmbeddings.Harrier"` — should probably be `"ElBruno.LocalEmbeddings.Harrier"` to match the package naming convention. The base uses `"LocalEmbeddings"`.
  - **Recommendation:** Use full `"ElBruno.LocalEmbeddings.Harrier"` for consistency with the `ElBruno.` naming convention.

### ❌ Bug/Issue

- **No bugs found.** The async factory + disposal pattern is clean.

---

## 5. Code Patterns Comparison with Base Library

### Differences Without Good Reason

| Pattern | Base Library | Harrier | Issue |
|---------|-------------|---------|-------|
| Linux ONNX alias workaround | `EnsureLinuxOnnxRuntimeAliases()` | Missing | Platform compatibility gap |
| DllNotFoundException handling | try/catch with diagnostic message | Missing | Poor error diagnostics on failure |
| Download serialization | `ConcurrentDictionary<SemaphoreSlim>` | None | Race condition risk |
| Sidecar hash on cache hit | Verified and re-downloads if invalid | File.Exists only | SEC-001 not fully applied |
| File move filter | `"*.onnx"` glob | All files | Overly broad |
| SharedHttpClient handler | `new HttpClient()` (pre-existing gap) | Same gap | Should fix in new code |
| File size guard | 50 MB for CLIP vocab (SEC-009) | None for tokenizer.json | Security gap |

### Code Duplication Candidates for Shared Utilities

1. **SHA-256 helpers:** `ComputeSha256` and `WriteSidecarHash` are duplicated identically between `ModelDownloader` and `HarrierModelDownloader`. Extract to a shared `HashHelper` or `SidecarHashHelper` utility in the base library.

2. **Path traversal guard:** The `SanitizeModelName` + `GetFullPath` + `StartsWith` guard is duplicated. Could become `PathGuard.ValidateCacheSubpath(cacheRoot, sanitizedName)`.

3. **ONNX SessionOptions construction:** Both `OnnxEmbeddingModel.Load()` and `HarrierOnnxEmbeddingModel.Load()` create identical `SessionOptions` blocks. Extract to a shared factory method.

4. **GenerateAsync boilerplate:** Both generators have identical `GenerateAsync` structure (materialize → empty check → tokenize → infer → wrap). Consider a base class or shared helper.

### Error Handling Consistency

- ✅ `ArgumentNullException.ThrowIfNull` — consistently used
- ✅ `ObjectDisposedException.ThrowIf` — consistently used
- ✅ `ArgumentException` for mismatched lengths — consistent messages
- ⚠️ Missing `DllNotFoundException` handling in Harrier's ONNX model load
- ⚠️ `FileNotFoundException` messages inconsistent: base uses "ONNX model file not found." + modelPath; Harrier matches this

---

## Summary of Critical Findings

| Severity | Count | Description |
|----------|-------|-------------|
| ❌ Bug | 1 | `Tokenize()` index-out-of-bounds when `maxLength=1` |
| ❌ Bug | 1 | Cache hit skips `.onnx_data` companion file verification |
| ⚠️ High | 1 | SentencePiece normalizer (space→▁) may be silently skipped by BpeTokenizer |
| ⚠️ High | 1 | No concurrent download serialization (race condition) |
| ⚠️ Medium | 1 | No sidecar hash verification on cache hit (SEC-001 gap) |
| ⚠️ Medium | 1 | Missing Linux ONNX Runtime alias workaround |
| ⚠️ Medium | 1 | Missing DllNotFoundException error handling |
| ⚠️ Medium | 1 | No tokenizer.json file size guard (SEC-009 gap) |
| ⚠️ Low | 5 | SharedHttpClient handler, merge parsing, CountTokens allocation, onnx/ cleanup, providerName |

**Overall Assessment:** The Harrier package is well-structured and follows base library patterns closely. The core ONNX inference path is correct and efficient. The two real bugs (index-out-of-bounds, missing .onnx_data check) are low-probability but should be fixed. The SentencePiece normalizer concern is the highest-risk item — if the BpeTokenizer doesn't apply the space→▁ mapping, embeddings will be subtly wrong. This needs empirical validation.
