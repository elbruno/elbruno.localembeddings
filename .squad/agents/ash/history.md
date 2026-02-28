# Ash — History & Learnings

## Project Context

- **Project:** ElBruno.LocalEmbeddings — a .NET library for local embedding generation using Microsoft.Extensions.AI and ONNX Runtime
- **Owner:** Bruno Capuano
- **Stack:** .NET 8.0 / 10.0 (multi-target), C#, Microsoft.Extensions.AI, ONNX Runtime, HuggingFace models, NuGet package distribution
- **Joined:** 2026-02-28

## Key Security Concerns for This Project

1. **Model downloads:** The library downloads ONNX model files from HuggingFace/CDNs over HTTPS. Integrity verification (hash/checksum) of downloaded models is a priority concern.
2. **File path handling:** Model cache paths are constructed from user-provided options — path traversal risk if inputs are not sanitized.
3. **NuGet dependencies:** SixLabors.ImageSharp had known vulnerabilities in 3.1.6 and 3.1.7 (CVE-level advisories). The team upgraded to 3.1.12. Ongoing advisory monitoring needed.
4. **Public NuGet package:** As a published library, security issues affect downstream consumers.

## Learnings

### 2026-05-31: Phase 1 Security Fixes Implemented

**SEC-001 (Model integrity):**
- SHA-256 sidecar files (`{model}.sha256`) are written after every successful download of ONNX files in both `ModelDownloader` and `HuggingFaceImageModelDownloader`.
- On cache hit, sidecar is verified; mismatch deletes the corrupt file and triggers re-download automatically.
- Legacy cached files (no sidecar) are treated as valid to preserve backward compatibility, and a sidecar is written on the next call.
- `LocalEmbeddingsOptions.ExpectedHash` (nullable `string?`) added — when set, the primary ONNX file's SHA-256 is asserted post-download.
- `SHA256.HashData(stream)` preferred over `SHA256.Create()` pattern (available .NET 5+, cleaner).

**SEC-006 (Path traversal):**
- `Path.GetFullPath` + `StartsWith(cacheRoot, OrdinalIgnoreCase)` guard added after `SanitizeModelName` in `ModelDownloader.EnsureModelAsync`.
- The guard runs before any directory creation or file I/O.

**Interface evolution note:**
- Adding an optional parameter to an interface method is backward-compatible for callers in C#, but all concrete implementations must be updated. Only one implementation (`ModelDownloader`) exists here, so the change was low risk.


### 2026-02-28: Comprehensive Security Audit Completed

**Scope:** All 5 source projects, 21 csproj files, full public API surface.

**Key findings (9 total):**
- **SEC-001 (HIGH):** No hash/checksum verification for downloaded ONNX models from HuggingFace. Both `ModelDownloader` and `HuggingFaceImageModelDownloader` accept files on HTTPS trust alone. Cached files also lack integrity checks.
- **SEC-002 (MEDIUM):** Static `new HttpClient()` in `LocalEmbeddingGenerator.cs:24` and parameterless `ModelDownloader()` constructor bypass `IHttpClientFactory`. DI path correctly uses `AddHttpClient<>()`.
- **SEC-003 (MEDIUM):** `ImageEmbeddingsOptions` file name properties (`TextModelFileName`, etc.) allow path traversal via `Path.Combine` without validation.
- **SEC-004-005 (MEDIUM):** `ClipImageEncoder`, `ClipTextEncoder`, and `ImageSearchEngine` constructors/methods lack null/argument validation.
- **SEC-006 (MEDIUM):** Path sanitization for model cache delegated entirely to `ElBruno.HuggingFace.Downloader` package — no defense-in-depth validation in this codebase.
- **SEC-007-009 (LOW):** Sync-over-async patterns, OnnxRuntime 1.24.1 → 1.24.2 available, ClipTokenizer reads files without size limits.

**Positive findings:**
- `dotnet list package --vulnerable` reports zero CVEs across all projects.
- No secrets, API keys, or credentials in source code.
- SixLabors.ImageSharp 3.1.12 is latest version with no NuGet advisories.
- DI registration correctly uses `IHttpClientFactory`.
- Nullable reference types and warnings-as-errors enforced globally.

**Report written to:** `.squad/decisions/inbox/ash-security-audit-findings.md`
