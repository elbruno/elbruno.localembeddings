# Security Phase 1 — Implementation Complete

**By:** Ash (Security Engineer)  
**Date:** 2026-05-31  
**Status:** Implemented ✅

---

## SEC-001 — Model Download Integrity Verification

### What was implemented

- **`ModelDownloader.cs`**: After every successful download (and onnx subdir move), SHA-256 hashes are computed for all ONNX files present and written to sidecar files (`{file}.sha256`) next to each model file.
- **Cache hit verification**: On the next call when model files already exist, each ONNX file is checked against its sidecar. If the sidecar is present and the hash does not match, the corrupt file is deleted and re-downloaded automatically.
- **Backward compatibility**: If no sidecar exists (legacy cached file), the file is treated as valid and a sidecar is created on the next successful `EnsureModelAsync` call.
- **`LocalEmbeddingsOptions.ExpectedHash`** (new nullable `string?` property): When set, the primary ONNX model file is verified against this hash after download. A mismatch throws `InvalidOperationException` with expected vs. actual hashes in the message.
- **`LocalEmbeddingGenerator.cs`**: Both sync (`ResolveModelDirectory`) and async (`ResolveModelDirectoryAsync`) paths now forward `options.ExpectedHash` to `EnsureModelAsync`.
- **`HuggingFaceImageModelDownloader.cs`**: Same sidecar pattern applied — integrity check before download (corrupt files are deleted and re-downloaded), sidecar write after download + move.

### API change

`IModelDownloader.EnsureModelAsync` gains an optional `string? expectedHash = null` parameter between `progress` and `cancellationToken`. All existing callers continue to compile unchanged.

---

## SEC-006 — Path Traversal Defense for Model Names

### What was implemented

- **`ModelDownloader.EnsureModelAsync`**: After `DefaultPathHelper.SanitizeModelName(modelName)`, the resolved directory is canonicalised with `Path.GetFullPath` and asserted to start with the canonicalised cache root (case-insensitive). A crafted model name that escapes the cache directory throws `ArgumentException` before any I/O occurs.

---

## Build Status

`dotnet build` — **0 errors, 0 warnings** ✅

---

## Files Changed

| File | Change |
|------|--------|
| `src/ElBruno.LocalEmbeddings/Options/LocalEmbeddingsOptions.cs` | Added `ExpectedHash` property |
| `src/ElBruno.LocalEmbeddings/ModelDownloader.cs` | SEC-001 + SEC-006 implementation |
| `src/ElBruno.LocalEmbeddings/LocalEmbeddingGenerator.cs` | Forward `ExpectedHash` to downloader |
| `src/ElBruno.LocalEmbeddings.ImageEmbeddings.Downloader/HuggingFaceImageModelDownloader.cs` | SEC-001 sidecar pattern |
