# Security Audit: Harrier Package & Full Repository

**By:** Ash (Security Engineer)  
**Date:** 2026-06-01  
**Scope:** Full repository with focus on `ElBruno.LocalEmbeddings.Harrier`

---

## Executive Summary

The Harrier package is well-built and adopts most security patterns established in the base library. Zero known CVEs across all dependencies. No secrets in source. However, I found **2 medium** and **4 low** findings, plus several positive observations. The most impactful finding is that the Harrier downloader lacks cache-hit integrity verification (sidecar hash checking) — a pattern the base library already implements.

---

## 1. Dependency Vulnerabilities

### ✅ GOOD — `dotnet list package --vulnerable`: Zero CVEs

All 25 projects report zero vulnerable packages against the NuGet advisory database.

### 🟢 LOW — SEC-H01: OnnxRuntime 1.24.2 is behind latest (1.24.4)

**Current:** Microsoft.ML.OnnxRuntime 1.24.2 across all projects  
**Latest:** 1.24.4  
**Risk:** 1.24.3 and 1.24.4 contain bug fixes. No security-specific CVEs published, but staying current is best practice.

**Remediation:** Bump to 1.24.4 in all csproj files referencing OnnxRuntime.

### ✅ GOOD — ElBruno.HuggingFace.Downloader 0.5.0

No NuGet advisories. Package used consistently at 0.5.0 across all projects.

### ✅ GOOD — Microsoft.ML.Tokenizers 2.0.0

No NuGet advisories. Latest stable version.

### ✅ GOOD — SixLabors.ImageSharp 3.1.12

No NuGet advisories. Previous CVEs (3.1.6/3.1.7) were resolved in earlier audit.

---

## 2. Model Download Security (HarrierModelDownloader.cs)

### ✅ GOOD — HTTPS enforced

All HuggingFace downloads go through `HuggingFaceDownloader` which constructs `https://huggingface.co/` URLs. No HTTP fallback path exists.

### ✅ GOOD — Path traversal defense-in-depth

Lines 63-70: `Path.GetFullPath` + `StartsWith(cacheRoot, OrdinalIgnoreCase)` guard present, matching the base library's SEC-006 pattern. Fires before any I/O.

### ✅ GOOD — SHA-256 sidecar written after download

Lines 143-145: `WriteSidecarHash` correctly writes a `.sha256` sidecar after successful download, using `SHA256.HashData(stream)`.

### ✅ GOOD — ExpectedHash verification

Lines 147-156: When `options.ExpectedHash` is set, the downloaded file's SHA-256 is computed and compared (case-insensitive). Hash mismatch throws `InvalidOperationException`.

### 🟡 MEDIUM — SEC-H02: No sidecar hash verification on cache hit

**Location:** `HarrierModelDownloader.cs:78-85`  
**Issue:** When the model already exists on disk (cache hit), the code only checks `File.Exists(modelPath)` and `File.Exists(tokenizerPath)` — it does **not** verify the sidecar hash. A corrupted or tampered cached file will be used without detection.

The base library's `ModelDownloader` (lines 123-142) has `SidecarHashValid()` that reads the `.sha256` sidecar, recomputes the file hash, and deletes+re-downloads on mismatch. Harrier writes sidecars but never reads them.

**Impact:** A local attacker or malware could replace the cached ONNX model with a malicious one. The sidecar is present but never checked on subsequent loads.

**Remediation:** Add a `SidecarHashValid()` method matching the base library pattern. On cache hit, verify the sidecar hash and re-download if it fails. Continue to treat legacy files (no sidecar) as valid for backward compatibility.

### 🟡 MEDIUM — SEC-H03: No concurrent download serialization

**Location:** `HarrierModelDownloader.cs:59` (the full `EnsureModelAsync` method)  
**Issue:** The base `ModelDownloader` uses `ConcurrentDictionary<string, SemaphoreSlim> _downloadLocks` (line 38) to serialize concurrent downloads for the same model directory, preventing `.tmp` file conflicts and partial writes. `HarrierModelDownloader` has no such protection.

**Impact:** In multi-threaded scenarios (e.g., multiple DI service resolutions racing), two threads could simultaneously download and write to the same model directory, causing data corruption or partial files.

**Remediation:** Add a `ConcurrentDictionary<string, SemaphoreSlim>` download lock, matching the pattern in the base `ModelDownloader`.

### 🟢 LOW — SEC-H04: onnx/ file move has no glob filter

**Location:** `HarrierModelDownloader.cs:119` — `Directory.GetFiles(onnxSubDir)` (no filter)  
**Base library:** `ModelDownloader.cs:189` — `Directory.GetFiles(onnxSubDir, "*.onnx")` (filtered)

**Issue:** The Harrier downloader moves **all** files from the `onnx/` subdirectory, not just `.onnx` and `.onnx_data` files. This is functionally correct (Harrier needs `_data` files), but it's a wider attack surface — any unexpected file placed in `onnx/` gets moved.

**Impact:** Low risk since the directory is populated by the controlled `HuggingFaceDownloader`. However, defense-in-depth suggests filtering to expected extensions.

**Remediation:** Filter to `*.onnx` and `*_data` patterns, or enumerate only expected filenames (`onnxFileName` and `onnxDataFileName`).

### ✅ GOOD — HttpClient with SocketsHttpHandler

The secondary constructor (line 49) uses `SocketsHttpHandler { PooledConnectionLifetime = 2 min }`, matching the SEC-002 fix pattern.

---

## 3. Tokenizer Security (HarrierTokenizer.cs)

### 🟢 LOW — SEC-H05: No file size guard on tokenizer.json before parsing

**Location:** `HarrierTokenizer.cs:200` — `File.OpenRead(path)` with no size check  
**Issue:** A crafted `tokenizer.json` with an extremely large `vocab` or `merges` section could cause excessive memory allocation during JSON parsing and BPE tokenizer construction.

The base library has a 50 MB guard for `ClipTokenizer` vocab/merges files (SEC-009). `HarrierTokenizer` has no equivalent.

**Impact:** Local DoS if a malicious tokenizer.json is placed in the cache directory. Low probability since it requires local file write access to the cache.

**Remediation:** Add a file size guard (e.g., 100 MB) before `File.OpenRead`. Throw `InvalidOperationException` with file name and size if exceeded.

### ✅ GOOD — Safe JSON parsing

Lines 201-205: Uses `JsonDocument.Parse` with `JsonDocumentOptions` (streaming, read-only DOM). Does not use `JsonSerializer.Deserialize<T>` — no deserialization vulnerabilities or type confusion risks. `AllowTrailingCommas` and `CommentHandling.Skip` are safe and defensive.

### ✅ GOOD — No reflection-based deserialization

Vocab and merges are extracted via explicit `TryGetProperty`/`EnumerateArray` — no `JsonSerializer`, no `System.Text.Json` polymorphic deserialization, no type injection surface.

---

## 4. ONNX Model Loading Security

### ✅ GOOD — Model path validated before loading

`HarrierOnnxEmbeddingModel.Load()` (lines 56-63): `ArgumentException.ThrowIfNullOrWhiteSpace` + `File.Exists` guard before `InferenceSession` creation. Matches the SEC-004 pattern.

### 🟢 LOW — SEC-H06: .onnx_data files not independently validated

**Location:** `HarrierModelDownloader.cs:74` — `onnx_data` file is downloaded alongside the model  
**Issue:** External data files (`.onnx_data`) are downloaded and moved but receive no sidecar hash or integrity verification. The SHA-256 sidecar is only written for the primary `.onnx` file.

**Impact:** A tampered `_data` file could contain malicious weights loaded by ONNX Runtime without detection. This is an inherent ONNX Runtime risk — the runtime loads external data files referenced in the model graph without independent verification.

**Remediation:** Write sidecar hashes for `.onnx_data` files as well. Consider supporting `ExpectedHash` verification for the data file (or a combined manifest hash).

### ✅ GOOD — SessionOptions configured securely

Lines 78-84 in `HarrierOnnxEmbeddingModel`: Graph optimization enabled, thread counts validated, `ObjectDisposedException.ThrowIf` used, `using var sessionOptions` prevents leaks.

---

## 5. Input Validation on Public APIs

### ✅ GOOD — Comprehensive null/argument validation

- `HarrierModelDownloader`: `ArgumentNullException.ThrowIfNull` for both constructor parameters
- `HarrierTokenizer.Create`: `string.IsNullOrWhiteSpace` check, `FileNotFoundException`, `ArgumentOutOfRangeException` for maxLength
- `HarrierTokenizer.Tokenize`: `ArgumentNullException.ThrowIfNull(text)`, `ArgumentOutOfRangeException` for maxLength
- `HarrierTokenizer.TokenizeBatch`: `ArgumentNullException.ThrowIfNull(texts)`
- `HarrierOnnxEmbeddingModel.Load`: Full validation chain (disposed, empty path, file exists, already loaded, thread counts)
- `HarrierOnnxEmbeddingModel.GenerateEmbedding(s)`: Disposed check, null checks, length consistency checks
- `HarrierEmbeddingGenerator`: `ArgumentNullException.ThrowIfNull(options)` and `(values)`, `ObjectDisposedException.ThrowIf`

### ✅ GOOD — MaxSequenceLength enforced

`HarrierTokenizer.Tokenize` enforces the configured max length via `EncodeToIds(inputText, contentMaxLength, ...)` with BOS/EOS reservation. The tokenizer caps output at the configured length regardless of input size.

### ✅ GOOD — CancellationToken threaded through all APIs

`TokenizeBatch`, `GenerateEmbeddings`, `EnsureModelAsync`, `GenerateAsync` all check `cancellationToken.ThrowIfCancellationRequested()` at loop boundaries.

---

## 6. Secrets and Sensitive Data

### ✅ GOOD — No hardcoded secrets

Grep for `apikey`, `api_key`, `secret`, `password`, `credential`, `bearer` returns zero matches across all source files.

### ✅ GOOD — .gitignore covers sensitive patterns

Lines 102-106: `appsettings.Development.json`, `appsettings.Local.json`, `secrets.json`, `*.pfx`, `*.p12` all excluded.

### ✅ GOOD — No PII in committed files

No user-identifying information, emails, or personal data found in source.

---

## 7. Cross-Package Comparison: Harrier vs. Base Library

| Security Feature | Base Library | Harrier | Status |
|---|---|---|---|
| Path traversal guard (GetFullPath + StartsWith) | ✅ | ✅ | Matched |
| SHA-256 sidecar write after download | ✅ | ✅ | Matched |
| SHA-256 sidecar check on cache hit | ✅ | ❌ | **SEC-H02** |
| ExpectedHash verification | ✅ | ✅ | Matched |
| Concurrent download serialization | ✅ | ❌ | **SEC-H03** |
| SocketsHttpHandler in non-DI constructor | ✅ | ✅ | Matched |
| File.Exists before InferenceSession | ✅ | ✅ | Matched |
| ArgumentNullException.ThrowIfNull | ✅ | ✅ | Matched |
| Tokenizer file size guard | ✅ (50 MB) | ❌ | **SEC-H05** |
| onnx/ file move filter | `*.onnx` only | All files | **SEC-H04** |
| Data file integrity verification | N/A | ❌ | **SEC-H06** |
| CancellationToken support | ✅ | ✅ | Matched |
| ArrayPool for batch inference | ✅ | ✅ | Matched |
| `using var sessionOptions` | ✅ | ✅ | Matched |

### Harrier improvements that base library should adopt:

- None identified. Harrier follows the base library patterns.

### Base library patterns that Harrier is missing:

- Sidecar hash verification on cache hit (SEC-H02)
- Concurrent download serialization (SEC-H03)
- Tokenizer file size guard (SEC-H05)

---

## 8. Additional Observation: Static HttpClient in HarrierEmbeddingGenerator

**Location:** `HarrierEmbeddingGenerator.cs:26`  
```csharp
private static readonly HttpClient SharedModelDownloadHttpClient = new();
```

This uses a bare `new HttpClient()` without `SocketsHttpHandler`, unlike the `HarrierModelDownloader` convenience constructor (which uses `SocketsHttpHandler`). The `SharedModelDownloadHttpClient` is passed to `HarrierModelDownloader(HttpClient, options)` which bypasses the handler. The base library's `LocalEmbeddingGenerator.cs:24` has the same pattern — this was previously noted as SEC-002 but only the `ModelDownloader()` parameterless constructor was fixed.

**Severity:** 🟢 LOW — DNS rotation and socket exhaustion risk for long-lived processes. The DI path through `ServiceCollectionExtensions.AddHarrierEmbeddings()` creates the generator via `CreateAsync()` which also uses this static client.

---

## Findings Summary

| ID | Severity | Description | Effort |
|----|----------|-------------|--------|
| SEC-H01 | 🟢 LOW | OnnxRuntime 1.24.2 → 1.24.4 available | Trivial |
| SEC-H02 | 🟡 MEDIUM | No sidecar hash verification on cache hit | Small — add `SidecarHashValid()` |
| SEC-H03 | 🟡 MEDIUM | No concurrent download serialization | Small — add SemaphoreSlim lock |
| SEC-H04 | 🟢 LOW | onnx/ file move has no glob filter | Trivial |
| SEC-H05 | 🟢 LOW | No tokenizer.json file size guard | Trivial |
| SEC-H06 | 🟢 LOW | .onnx_data files not integrity-verified | Small |

**Positive findings:** 14 items verified as correctly implemented (see ✅ items above).

---

## Recommended Remediation Priority

1. **SEC-H02** (sidecar check on cache hit) — Highest priority. Without this, the integrity verification system is write-only. One-time fix, small code addition.
2. **SEC-H03** (download serialization) — Important for multi-threaded scenarios. Add `ConcurrentDictionary<string, SemaphoreSlim>` pattern.
3. **SEC-H01** (OnnxRuntime bump) — Routine maintenance, no urgency.
4. **SEC-H05** (tokenizer size guard) — Quick hardening.
5. **SEC-H04** (file move filter) — Minor hardening.
6. **SEC-H06** (data file hashing) — Nice-to-have, requires design decision on manifest approach.
