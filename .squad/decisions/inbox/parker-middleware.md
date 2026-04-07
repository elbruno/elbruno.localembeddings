# Parker — M.E.AI Middleware and Batch Auto-Tuning

**Date:** 2026-04-04  
**By:** Parker (Performance Engineer)  
**Status:** Implemented

## Context

Roadmap items 4.1 (M.E.AI middleware support) and 5.3 (batch size auto-tuning) identified middleware integration and adaptive batching as priorities for improving observability and throughput optimization.

## Decisions

### 1. Middleware Implementation Pattern

**Decision:** Use `DelegatingEmbeddingGenerator<string, Embedding<float>>` base class from `Microsoft.Extensions.AI.Abstractions` for middleware, NOT the full `Microsoft.Extensions.AI` package's builder infrastructure.

**Rationale:**
- `DelegatingEmbeddingGenerator` is in Abstractions (already referenced)
- Builder pattern (`EmbeddingGeneratorBuilder`) is in full M.E.AI package (would add dependency)
- Extension method decorator pattern (`generator.UseRetry().UseOpenTelemetry()`) is simpler and more composable
- Follows existing codebase pattern seen in `CachingEmbeddingDecorator.cs`

**Alternative considered:** Using `Microsoft.Extensions.AI`'s built-in `OpenTelemetryEmbeddingGenerator` — rejected because it would require adding the full M.E.AI package just for middleware, increasing package footprint.

### 2. OpenTelemetry Activity Source Name

**Decision:** Use `"ElBruno.LocalEmbeddings"` as the ActivitySource name (matches root namespace).

**Rationale:**
- Standard .NET convention: ActivitySource name = assembly/namespace
- Enables filtering: users can subscribe to activities from this library specifically
- Consistent with library naming (`ElBruno.LocalEmbeddings.*`)

### 3. Retry Middleware Scope

**Decision:** Only retry `OnnxRuntimeException` and `IOException`, NOT all exceptions.

**Rationale:**
- ONNX runtime can have transient model loading/inference failures
- File I/O (model loading, cache access) can have transient failures (network drives, locks)
- Argument validation errors (`ArgumentException`, `ArgumentNullException`) should NOT be retried — they indicate caller bugs
- Cancellation (`OperationCanceledException`) should NOT be retried — user requested stop

### 4. Batch Size Auto-Tuning Integration

**Decision:** Implement auto-tuner infrastructure but defer integration into `GenerateEmbeddings` until tests exist.

**Rationale:**
- Auto-tuner logic is non-trivial (GC monitoring, throughput measurement, diminishing returns)
- Current batch logic in `OnnxEmbeddingModel` is working and tested
- Integration requires caching the determined batch size (state management)
- Better to deliver infrastructure cleanly, integrate in a separate PR with tests

**Implementation strategy for future integration:**
```csharp
// In OnnxEmbeddingModel (pseudo-code)
private int? _cachedOptimalBatchSize;

if (options.BatchSizeMode == BatchSizeMode.Auto && _cachedOptimalBatchSize is null)
{
    var tuner = new BatchSizeAutoTuner();
    _cachedOptimalBatchSize = tuner.DetermineBatchSize(
        options.MinBatchSize, 
        options.MaxBatchSize,
        batchSize => RunInferenceBatch(sampleInputs, batchSize));
}

int effectiveBatchSize = options.BatchSizeMode == BatchSizeMode.Auto 
    ? _cachedOptimalBatchSize!.Value 
    : options.BatchSize;
```

### 5. Package Dependency Additions

**Decision:** Add `System.Diagnostics.DiagnosticSource 10.0.5` for `ActivitySource` support.

**Rationale:**
- Required for `Activity` and `ActivitySource` (OpenTelemetry tracing infrastructure)
- Version 10.0.5 matches other `System.*` and `Microsoft.Extensions.*` packages in the csproj
- Lightweight — no additional transitive dependencies beyond what .NET runtime provides

## Impact

**For consumers:**
- Middleware enables zero-config OpenTelemetry integration: `new LocalEmbeddingGenerator().UseOpenTelemetry()`
- Retry middleware improves resilience: `generator.UseRetry(maxRetries: 5)`
- Batch auto-tuning options available for future use (no breaking changes)

**For library maintainers:**
- New public API surface: `OpenTelemetryEmbeddingMiddleware`, `RetryEmbeddingMiddleware`, `EmbeddingMiddlewareExtensions`
- New options: `BatchSizeMode`, `BatchSize`, `MinBatchSize`, `MaxBatchSize` in `LocalEmbeddingsOptions`
- Internal auto-tuner ready for integration when batch logic tests are available

**Package impact:**
- +1 package reference (`System.Diagnostics.DiagnosticSource`)
- No breaking changes — all new features are opt-in

## Testing Needs

1. **Middleware tests (deferred to Lambert):**
   - Verify Activity spans are created with correct tags
   - Verify retry backoff timing (exponential: 200ms, 400ms, 800ms)
   - Verify retry only triggers on transient exceptions
   - Verify non-retriable exceptions bubble immediately

2. **Auto-tuner tests (deferred to Lambert):**
   - Unit test: verify doubling stops at diminishing returns (<10% improvement)
   - Unit test: verify GC pressure detection (>2 Gen2 collections → backoff)
   - Integration test: profile real ONNX inference (requires model download in CI)

3. **Integration tests (future):**
   - Verify auto-tuned batch size matches or exceeds fixed batch throughput
   - Verify cached batch size is reused across calls

## Related Roadmap Items

- ✅ **4.1 M.E.AI Middleware Extensions** — Completed
- ✅ **5.3 Batch Size Auto-Tuning** — Infrastructure complete, integration deferred
- Related: **4.3 Streaming embeddings** (Dallas WIP) — may influence batch tuning integration
- Related: **4.4 Embedding cache** (Kane completed) — may interact with auto-tuned batch size in cache key/invalidation

## Files Changed

- **New:**
  - `src/ElBruno.LocalEmbeddings/Middleware/OpenTelemetryEmbeddingMiddleware.cs`
  - `src/ElBruno.LocalEmbeddings/Middleware/RetryEmbeddingMiddleware.cs`
  - `src/ElBruno.LocalEmbeddings/Middleware/EmbeddingMiddlewareExtensions.cs`
  - `src/ElBruno.LocalEmbeddings/BatchSizeMode.cs`
  - `src/ElBruno.LocalEmbeddings/BatchSizeAutoTuner.cs`
- **Modified:**
  - `src/ElBruno.LocalEmbeddings/Options/LocalEmbeddingsOptions.cs` — added 4 batch size properties
  - `src/ElBruno.LocalEmbeddings/ElBruno.LocalEmbeddings.csproj` — added DiagnosticSource package reference

## Build Verification

```
dotnet build src/ElBruno.LocalEmbeddings/ElBruno.LocalEmbeddings.csproj --configuration Release
```

✅ Build succeeded (net8.0 + net10.0) — 0 warnings, 0 errors  
✅ TreatWarningsAsErrors enforced  
✅ All new types follow codebase conventions (sealed, XML docs, file-scoped namespaces)
