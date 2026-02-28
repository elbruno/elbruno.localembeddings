# Decision: Phase 4 Security Polish Implemented

**By:** Ash (Security Engineer)  
**Date:** 2026-06-XX  
**Status:** Implemented

## Summary

Four low/medium security fixes applied as Phase 4 security polish.

## Changes Made

### SEC-002 — HttpClient PooledConnectionLifetime (`ModelDownloader.cs`)

The parameterless `ModelDownloader()` constructor previously called `new HttpClient()` directly, risking socket exhaustion in long-running processes. Updated to:

```csharp
public ModelDownloader() : this(
    new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) }),
    null)
```

XML doc comment updated to steer production users toward the DI / `IHttpClientFactory` path.

### SEC-007 — Sync-over-async warning (`LocalEmbeddingGenerator.cs`)

Added `<remarks>` block to the `LocalEmbeddingGenerator(LocalEmbeddingsOptions)` constructor warning about potential deadlocks in ASP.NET Core / async-first environments. Added inline comment at the `.GetAwaiter().GetResult()` call site.

### SEC-008 — OnnxRuntime bumped 1.24.1 → 1.24.2

Bumped `Microsoft.ML.OnnxRuntime` to 1.24.2 (latest patch) in:
- `src/ElBruno.LocalEmbeddings/ElBruno.LocalEmbeddings.csproj`
- `src/ElBruno.LocalEmbeddings.ImageEmbeddings/ElBruno.LocalEmbeddings.ImageEmbeddings.csproj`
- `tests/ElBruno.LocalEmbeddings.Tests/ElBruno.LocalEmbeddings.Tests.csproj`

The test project also pinned the package directly; not bumping it caused NU1605 (package downgrade warning-as-error). No other packages changed.

### SEC-009 — ClipTokenizer file size guard (`ClipTokenizer.cs`)

Added a 50 MB size guard for both the vocab JSON and merges text files before reading them, preventing OOM from maliciously oversized files:

```csharp
const long MaxVocabFileSizeBytes = 50 * 1024 * 1024; // 50 MB
var vocabFileInfo = new FileInfo(vocabJsonPath);
if (vocabFileInfo.Length > MaxVocabFileSizeBytes)
    throw new InvalidOperationException(...);
```

## Build Result

`dotnet build` — **0 errors, 0 warnings**.
