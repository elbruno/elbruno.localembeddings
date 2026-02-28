# Security Audit Report — ElBruno.LocalEmbeddings

**Auditor:** Ash (Security Engineer)  
**Date:** 2026-02-28  
**Scope:** All source projects, dependencies, public API surface  
**Status:** Complete

---

## Executive Summary

Comprehensive audit of all 5 source projects, 21 csproj files, and all public API surface. **No known CVEs** were found in current dependencies (`dotnet list package --vulnerable` clean). **No secrets or credentials** were found in source code. Nine security findings were identified, with one HIGH severity issue related to model download integrity, and several MEDIUM issues around input validation and HTTP client lifecycle.

---

## Findings

### SEC-001: No integrity verification for downloaded model files

- **Severity:** HIGH
- **Location:** `src/ElBruno.LocalEmbeddings/ModelDownloader.cs:70-168`, `src/ElBruno.LocalEmbeddings.ImageEmbeddings.Downloader/HuggingFaceImageModelDownloader.cs:44-84`
- **Description:** ONNX model files downloaded from HuggingFace are accepted without any hash/checksum verification. The only protection is HTTPS transport security. Cached files are also used without integrity checks (existence-only check at lines 86-101 of ModelDownloader.cs).
- **Attack vector:** If a CDN is compromised, a corporate HTTPS-intercepting proxy substitutes content, or a local attacker tampers with cached model files, a malicious ONNX model could be loaded into ONNX Runtime, potentially leading to arbitrary code execution or data exfiltration through crafted model inference.
- **Recommended fix:**
  1. Add SHA-256 hash verification after download, comparing against known-good hashes from HuggingFace model cards or a manifest file.
  2. Add hash verification on cache read (at minimum, store and re-verify the hash from initial download).
  3. Consider adding a `ModelHashes` dictionary option to `LocalEmbeddingsOptions` for users to pin expected hashes.
- **Test required:**
  - Unit test: Verify download fails when hash doesn't match expected value.
  - Unit test: Verify cached file is rejected when its hash changes after initial download.
  - Integration test: Verify end-to-end download + hash verification with a known model.

---

### SEC-002: Static HttpClient instantiation bypasses DNS rotation

- **Severity:** MEDIUM
- **Location:** `src/ElBruno.LocalEmbeddings/LocalEmbeddingGenerator.cs:24` — `private static readonly HttpClient SharedModelDownloadHttpClient = new();`
- **Location:** `src/ElBruno.LocalEmbeddings/ModelDownloader.cs:42-44` — parameterless constructor `public ModelDownloader() : this(new HttpClient(), null)`
- **Description:** Direct `new HttpClient()` instantiation outside of `IHttpClientFactory` does not participate in DNS rotation and can hold stale DNS entries. The static instance in `LocalEmbeddingGenerator` partially mitigates socket exhaustion (single instance reused) but never refreshes DNS. The parameterless `ModelDownloader()` constructor creates a fresh `HttpClient` per call.
- **Attack vector:** In long-running applications, stale DNS entries could cause connections to go to decommissioned servers. The parameterless constructor, if called repeatedly, causes socket exhaustion (port exhaustion DoS).
- **Recommended fix:**
  1. Remove the parameterless `ModelDownloader()` constructor or mark it `[Obsolete]` with guidance to use DI.
  2. Consider using `SocketsHttpHandler` with `PooledConnectionLifetime` for the static `SharedModelDownloadHttpClient` to enable DNS rotation.
  3. Document that direct instantiation is for simple/short-lived scenarios only.
- **Test required:**
  - Unit test: Verify `ModelDownloader` created via DI uses `IHttpClientFactory`-managed client.
  - Unit test: Verify the static HttpClient in `LocalEmbeddingGenerator` has appropriate handler configuration.

---

### SEC-003: Path traversal via configurable model file names in ImageEmbeddingsOptions

- **Severity:** MEDIUM
- **Location:** `src/ElBruno.LocalEmbeddings.ImageEmbeddings/Options/ImageEmbeddingsOptions.cs:16-24` — `TextModelFileName`, `VisionModelFileName`, `VocabFileName`, `MergesFileName` properties
- **Location:** `src/ElBruno.LocalEmbeddings.ImageEmbeddings/Options/ImageEmbeddingsOptions.cs:41-56` — `Path.Combine(ModelDirectory, TextModelFileName)` and similar
- **Description:** The file name properties accept arbitrary strings and are used directly in `Path.Combine`. If set to values like `../../etc/passwd` or an absolute path, `Path.Combine` will produce a path outside the intended `ModelDirectory`.
- **Attack vector:** In scenarios where configuration is sourced from untrusted inputs (environment variables, user-supplied config files, API parameters), an attacker could force the library to load arbitrary files as ONNX models, potentially causing crashes or information disclosure.
- **Recommended fix:**
  1. Add validation in the computed path properties (`TextModelPath`, etc.) to ensure the resolved path starts with `ModelDirectory` using `Path.GetFullPath` comparison.
  2. Reject file names containing path separators (`/`, `\`) or `..` sequences.
  3. Alternatively, validate in the `set` accessor of each file name property.
- **Test required:**
  - Unit test: Verify `TextModelFileName = "../../etc/passwd"` throws `ArgumentException`.
  - Unit test: Verify `TextModelFileName = "/absolute/path"` throws `ArgumentException`.
  - Unit test: Verify legitimate file names like `"custom_model.onnx"` are accepted.

---

### SEC-004: Missing input validation in ClipImageEncoder and ClipTextEncoder constructors

- **Severity:** MEDIUM
- **Location:** `src/ElBruno.LocalEmbeddings.ImageEmbeddings/ClipImageEncoder.cs:33-37`
- **Location:** `src/ElBruno.LocalEmbeddings.ImageEmbeddings/ClipTextEncoder.cs:28-31`
- **Description:** Constructor parameters (`modelPath`, `vocabPath`, `mergesPath`) lack null/empty checks and file existence validation. Passing null or non-existent paths produces generic ONNX Runtime or file system errors instead of clear `ArgumentNullException` or `FileNotFoundException`.
- **Attack vector:** Primarily a robustness issue. Null inputs cause `NullReferenceException` crashes; non-existent paths produce unclear errors that complicate debugging and could mask configuration attacks.
- **Recommended fix:**
  ```csharp
  // In ClipImageEncoder constructor:
  ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
  if (!File.Exists(modelPath))
      throw new FileNotFoundException("CLIP vision model file not found.", modelPath);
  
  // Similar for ClipTextEncoder with all three parameters.
  ```
- **Test required:**
  - Unit test: Verify `ClipImageEncoder(null)` throws `ArgumentNullException`.
  - Unit test: Verify `ClipImageEncoder("")` throws `ArgumentException`.
  - Unit test: Verify `ClipImageEncoder("/nonexistent/path")` throws `FileNotFoundException`.
  - Same pattern for `ClipTextEncoder`.

---

### SEC-005: Missing null argument checks in ImageSearchEngine

- **Severity:** MEDIUM
- **Location:** `src/ElBruno.LocalEmbeddings.ImageEmbeddings/ImageSearchEngine.cs:25-29` — constructor
- **Location:** `src/ElBruno.LocalEmbeddings.ImageEmbeddings/ImageSearchEngine.cs:87` — `SearchByText(string query, ...)`
- **Location:** `src/ElBruno.LocalEmbeddings.ImageEmbeddings/ImageSearchEngine.cs:104` — `SearchByImage(string imagePath, ...)`
- **Description:** The constructor does not validate `imageEncoder` and `textEncoder` for null. `SearchByText` does not validate the `query` parameter. `SearchByImage` does not validate `imagePath` or check file existence before encoding.
- **Attack vector:** Null parameters cause `NullReferenceException` at unexpected points during operation. Missing file path validation in `SearchByImage` leads to unclear errors from ImageSharp.
- **Recommended fix:**
  ```csharp
  // Constructor:
  ArgumentNullException.ThrowIfNull(imageEncoder);
  ArgumentNullException.ThrowIfNull(textEncoder);
  
  // SearchByText:
  ArgumentNullException.ThrowIfNull(query);
  
  // SearchByImage:
  ArgumentNullException.ThrowIfNull(imagePath);
  if (!File.Exists(imagePath))
      throw new FileNotFoundException("Image file not found.", imagePath);
  ```
- **Test required:**
  - Unit test: Verify constructor throws `ArgumentNullException` for null encoder parameters.
  - Unit test: Verify `SearchByText(null)` throws `ArgumentNullException`.
  - Unit test: Verify `SearchByImage(null)` throws `ArgumentNullException`.

---

### SEC-006: Path sanitization delegated to external package without verification

- **Severity:** MEDIUM
- **Location:** `src/ElBruno.LocalEmbeddings/ModelDownloader.cs:77` — `DefaultPathHelper.SanitizeModelName(modelName)`
- **Description:** Model name sanitization for cache directory paths is performed by `DefaultPathHelper.SanitizeModelName()` from the `ElBruno.HuggingFace.Downloader` (v0.5.0) external package. The team decision notes "slashes → underscores" but the actual implementation cannot be verified from this codebase. If the sanitization is incomplete (e.g., doesn't handle `..`, null bytes, or OS-specific separators), path traversal is possible.
- **Attack vector:** A malicious model name like `"../../../etc"` or `"model\0name"` could escape the cache directory if sanitization is incomplete, writing files to arbitrary locations.
- **Recommended fix:**
  1. Add a secondary validation after `SanitizeModelName`: verify the resulting `modelDirectory` path starts with `_cacheDirectory` using `Path.GetFullPath`.
  2. Consider wrapping the sanitization with an explicit allowlist check (alphanumeric, hyphens, underscores only).
  ```csharp
  var sanitizedName = DefaultPathHelper.SanitizeModelName(modelName);
  var modelDirectory = Path.GetFullPath(Path.Combine(_cacheDirectory, sanitizedName));
  if (!modelDirectory.StartsWith(Path.GetFullPath(_cacheDirectory), StringComparison.OrdinalIgnoreCase))
      throw new ArgumentException("Model name resolves to a path outside the cache directory.", nameof(modelName));
  ```
- **Test required:**
  - Unit test: Verify model name `"../../escape"` is rejected or safely sanitized.
  - Unit test: Verify model name with null bytes is rejected.
  - Unit test: Verify legitimate model names like `"sentence-transformers/all-MiniLM-L6-v2"` work correctly.

---

### SEC-007: Sync-over-async patterns risk deadlock (DoS)

- **Severity:** LOW
- **Location:** `src/ElBruno.LocalEmbeddings/LocalEmbeddingGenerator.cs:271` — `downloader.EnsureModelAsync(...).GetAwaiter().GetResult()`
- **Location:** `src/ElBruno.LocalEmbeddings.ImageEmbeddings/Extensions/ServiceCollectionExtensions.cs:89` — `downloader.EnsureModelDownloadedAsync(...).GetAwaiter().GetResult()`
- **Description:** Synchronous blocking on async methods via `.GetAwaiter().GetResult()` can deadlock in contexts with a synchronization context (ASP.NET pre-Core, UI applications, certain test frameworks).
- **Attack vector:** In specific hosting contexts, model download during startup deadlocks the application, causing a denial-of-service condition.
- **Recommended fix:** The `LocalEmbeddingGenerator` already provides `CreateAsync()` factory methods. Document that the sync constructor should only be used in console/background contexts. For the Image Embeddings DI registration, consider an async initialization pattern.
- **Test required:**
  - Integration test: Verify `CreateAsync` completes without deadlock in an ASP.NET-like context.
  - Documentation: Add warning to sync constructor XML docs about deadlock risk.

---

### SEC-008: OnnxRuntime minor version behind

- **Severity:** LOW
- **Location:** `src/ElBruno.LocalEmbeddings/ElBruno.LocalEmbeddings.csproj:27`, `src/ElBruno.LocalEmbeddings.ImageEmbeddings/ElBruno.LocalEmbeddings.ImageEmbeddings.csproj:29`
- **Description:** Microsoft.ML.OnnxRuntime 1.24.1 is installed; version 1.24.2 is available. While no known CVEs affect 1.24.1 per NuGet advisories, staying current with ONNX Runtime is important since it loads and executes untrusted model files.
- **Attack vector:** If a vulnerability is discovered in ONNX Runtime 1.24.1 model parsing, this library would be exposed until updated.
- **Recommended fix:** Update to `Microsoft.ML.OnnxRuntime` 1.24.2 in both csproj files.
- **Test required:** Run existing test suite after version bump to verify no regressions.

---

### SEC-009: ClipTokenizer reads entire file into memory without size limits

- **Severity:** LOW
- **Location:** `src/ElBruno.LocalEmbeddings.ImageEmbeddings/ClipTokenizer.cs:28` — `File.ReadAllText(vocabJsonPath)`
- **Description:** The vocabulary JSON file is read entirely into memory and deserialized without any size limit check. A maliciously large vocabulary file could cause out-of-memory conditions.
- **Attack vector:** If an attacker can control the vocabulary file path (via `ImageEmbeddingsOptions.VocabFileName`), they could point to a very large file causing OOM → DoS.
- **Recommended fix:** Add a file size check before reading (e.g., reject files > 100MB) and validate that the path is within the expected model directory.
- **Test required:**
  - Unit test: Verify excessively large vocabulary file is rejected.

---

## Positive Findings

| Area | Status |
|------|--------|
| **No known CVEs in dependencies** | ✅ `dotnet list package --vulnerable` reports zero vulnerabilities |
| **No secrets/credentials in source** | ✅ No API keys, tokens, passwords, or hardcoded credentials found |
| **SixLabors.ImageSharp 3.1.12** | ✅ Latest available version, no NuGet advisories |
| **DI path uses IHttpClientFactory** | ✅ `ServiceCollectionExtensions.AddLocalEmbeddingsCore()` correctly uses `AddHttpClient<>()` |
| **Nullable reference types enabled** | ✅ Enforced project-wide via `Directory.Build.props` |
| **Warnings as errors** | ✅ `TreatWarningsAsErrors=true` catches many issues at build time |
| **HTTPS download URLs** | ✅ HuggingFace URLs use HTTPS scheme |
| **Temp file safety** | ✅ No `Path.GetTempFileName` or temp file usage found in source (delegated to HuggingFace.Downloader) |

---

## Remediation Priority

| Priority | Finding | Effort |
|----------|---------|--------|
| 1 | SEC-001: Model download integrity verification | Medium — requires hash infrastructure |
| 2 | SEC-006: Path traversal defense-in-depth for model names | Low — add `Path.GetFullPath` guard |
| 3 | SEC-003: ImageEmbeddingsOptions filename validation | Low — add path validation |
| 4 | SEC-004: ClipImageEncoder/ClipTextEncoder input validation | Low — add null/existence checks |
| 5 | SEC-005: ImageSearchEngine null checks | Low — add ArgumentNullException guards |
| 6 | SEC-002: HttpClient lifecycle improvement | Low — configure `PooledConnectionLifetime` |
| 7 | SEC-008: OnnxRuntime version bump | Trivial — update version number |
| 8 | SEC-007: Document sync-over-async risks | Trivial — XML doc updates |
| 9 | SEC-009: ClipTokenizer file size check | Low — add size guard |

---

## Recommendations for Remediation Plan

1. **Phase 1 (Critical Path):** Address SEC-001 (model integrity) and SEC-006 (path traversal) — these are the highest-impact issues affecting supply chain security.
2. **Phase 2 (Hardening):** Address SEC-002 through SEC-005 — input validation and resource management improvements.
3. **Phase 3 (Maintenance):** Address SEC-007 through SEC-009 — version bumps, documentation, and minor hardening.

Each fix should be accompanied by the specified unit tests. All fixes should be verified against the existing test suite to prevent regressions.
