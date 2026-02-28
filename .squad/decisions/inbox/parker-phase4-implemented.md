# Parker — Phase 4 Async Pattern Polish — Implemented

**By:** Parker (Performance Engineer)  
**Date:** 2025-07-16  
**Status:** Implemented

## Summary

Completed PERF-04/05 (async factory documentation) and a residual allocation fix for `GenerateAsync`.

## Changes Made

### 1. `src/ElBruno.LocalEmbeddings/Extensions/ServiceCollectionExtensions.cs`

- **`AddLocalEmbeddings(services, configure)` public overload**: Expanded `<remarks>` to explain that
  the DI factory calls the `LocalEmbeddingGenerator` constructor which performs sync-over-async
  model download when `EnsureModelDownloaded = true`. Added a two-part `<example>` block showing
  (a) standard DI usage and (b) the fully async pre-build pattern using `CreateAsync()` before
  `builder.Build()` with `AddSingleton(generator)`.

- **`AddLocalEmbeddingsCore` private method**: Replaced the terse comment on the singleton factory
  with an explicit inline warning noting the sync-over-async risk and directing developers to the
  `CreateAsync()` alternative.

### 2. `src/ElBruno.LocalEmbeddings/LocalEmbeddingGenerator.cs`

- **`CreateAsync(LocalEmbeddingsOptions, CancellationToken)` overload**: Replaced the minimal remarks
  with a fuller explanation covering when to prefer `CreateAsync` over the constructor, and added a
  complete `<example>` code block showing the recommended async DI pattern (call `CreateAsync` before
  building the host, register the result as a singleton).

- **`GenerateAsync` allocation fix (PERF residual)**: Changed `var valuesList = values.ToList();`
  to `IList<string> valuesList = values as IList<string> ?? values.ToList();` — consistent with
  the Phase 3 pattern applied to `Tokenizer.TokenizeBatch`. When callers pass a `List<string>` (the
  common case), this avoids one heap allocation per `GenerateAsync` call.

### 3. `src/ElBruno.LocalEmbeddings.ImageEmbeddings/Extensions/ServiceCollectionExtensions.cs`

- **`AddImageEmbeddings(services, configure)` public overload**: Added an `<strong>Async-Safety
  Note</strong>` paragraph to `<remarks>` explaining that when `EnsureModelDownloaded = true` the
  singleton factory blocks on download (sync-over-async), and advising that these singletons should
  be resolved at startup rather than on hot paths.

- **`EnsureModels` private method**: Updated the inline comment on
  `downloader.EnsureModelDownloadedAsync(...).GetAwaiter().GetResult()` to explicitly label it as
  "Sync-over-async" so readers immediately understand the pattern.

## Remaining Allocation Scan

Checked `LocalEmbeddingGenerator.cs` for `.ToList()` / `.ToArray()` calls after the Phase 3 pass:

- `values.ToList()` on line 180 — **fixed** (see above).
- No other instances found. The `rawEmbeddings.Select(...)` `.ToList()` noted in Phase 3 history
  was already removed. No further allocation changes needed.

## Build Result

`dotnet build` — **succeeded**, 0 warnings, 0 errors, across all target frameworks (net8.0 + net10.0).

## Decision

No architectural changes were made. The sync-over-async pattern in both `LocalEmbeddingGenerator`
(constructor) and `ImageEmbeddings` DI registration is preserved for backwards compatibility.
Documentation now clearly surfaces the risk and the `CreateAsync()` escape hatch for developers
who need fully non-blocking initialization.
