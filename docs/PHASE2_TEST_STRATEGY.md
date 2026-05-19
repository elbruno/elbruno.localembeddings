# Phase 2 Test Strategy — Enterprise Features

**Prepared by:** Lambert (Tester)  
**Date:** 2026-05-19  
**Scope:** Native AOT, Quantization, OpenTelemetry, Streaming API  
**Status:** ✅ Design Complete & Ready for Implementation

---

## Executive Summary

Phase 2 introduces four critical enterprise features to ElBruno.LocalEmbeddings:
1. **Native AOT Compatibility** — Trimmed, serverless-ready deployments
2. **Quantized Model Support** — INT8 inference with accuracy validation  
3. **OpenTelemetry Integration** — Structured observability with spans/metrics
4. **Streaming Embeddings API** — Memory-efficient 100K+ vector processing

This document defines **comprehensive test coverage** ensuring zero regressions and production-grade quality. All tests follow **xUnit + Moq + table-driven patterns** established in Phase 1.

---

## 1. Phase 1 Test Infrastructure (Reusable Patterns)

### Test Framework & Tools
- **Unit Framework:** xUnit (table-driven tests via `[Theory]` + `[InlineData]`)
- **Mocking:** Moq for dependencies (`IEmbeddingGenerator`, `ILogger`)
- **Test Types:**
  - **Unit Tests:** Isolated component logic (options parsing, enum validation)
  - **Integration Tests:** Multi-layer flows (e.g., quantized model loading → embedding generation)
  - **Smoke Tests:** End-to-end scenarios (AOT-compiled binary execution)
  - **Performance Baseline:** Latency, memory, throughput regression detection

### Existing Test Project Structure
```
tests/
├── ElBruno.LocalEmbeddings.Azure.Tests/
│   ├── HybridAzureEmbeddingGeneratorTests.cs      (hybrid fallback patterns)
│   └── ServiceCollectionExtensionsTests.cs         (DI registration)
└── [Phase 2 additions below]
```

### Phase 1 Patterns to Reuse
1. **Mocking external dependencies** (Azure clients, ONNX runtime)
2. **`[SkippableFact]` for conditional tests** (GPU/model availability)
3. **Table-driven `[Theory]`** for edge cases (e.g., various batch sizes)
4. **`Assert.ThrowsAsync<T>`** for exception validation
5. **`using` statements for resource cleanup** (sessions, generators)

---

## 2. Phase 2 Features & Test Scope

### 2.1 Native AOT Support (2.1)

**Feature Goal:** Core library trimming-safe; no reflection, dynamic code generation, or runtime type discovery.

#### Test Categories

**A. Unit Tests: Trimming Validation**

| Test ID | Scenario | Expected Result | Notes |
|---------|----------|-----------------|-------|
| AOT-U-001 | Verify no reflection calls in `OnnxEmbeddingModel.Load` | Compilation succeeds with `PublishTrimmed=true` | Use `IlLink` analyzer in build |
| AOT-U-002 | Verify `LocalEmbeddingsOptions` has `[DynamicallyAccessedMembers]` on public properties | No trimming warnings | Check .csproj for `<PublishTrimmed>true</PublishTrimmed>` |
| AOT-U-003 | Verify no `typeof(T).GetProperty()` usage in startup path | No `MethodNotFound` at runtime | Code review + static analyzer |
| AOT-U-004 | Test JSON serialization of options with nulls (`ModelPath=null`, `ExecutionProvider=default`) | Deserializes without errors | Use `System.Text.Json` with source generator |
| AOT-U-005 | Verify `SerializationContext` generated for all public types | Schema coverage 100% | MSBuild target checks `[JsonSerializable]` attributes |

**B. Integration Tests: AOT Model Loading**

| Test ID | Scenario | Expected Result | Notes |
|---------|----------|-----------------|-------|
| AOT-I-001 | Load model via `GenerateAsync` in trimmed runtime | Embeddings generated successfully | Requires test: publish as SCD, execute |
| AOT-I-002 | Load model with custom `ExecutionProvider` in trimmed runtime | Model initialized without reflection | Verify provider enum dispatch is compile-time |
| AOT-I-003 | Batch embedding generation (1K vectors) in trimmed runtime | O(buffer_size) memory, no growth | Memory profile test |
| AOT-I-004 | Async enumeration cleanup (cancel mid-stream) in trimmed runtime | Resources freed without finalizers | Test `IAsyncDisposable` finalization |

**C. End-to-End (E2E) Smoke Test**

| Test ID | Scenario | Expected Result | Notes |
|---------|----------|-----------------|-------|
| AOT-E2E-001 | Publish as Native AOT (self-contained, trimmed) | Binary size <100MB for MiniLM | Baseline: measure vs. framework-dependent |
| AOT-E2E-002 | Cold start (first embedding after app launch) | Latency <500ms (vs. 1-2s JIT) | Benchmark vs. baseline |
| AOT-E2E-003 | Memory footprint (idle + 10K embeddings) | <200MB (vs. 400MB+ JIT) | Peak working set measurement |
| AOT-E2E-004 | No reflection warnings during publish | 0 trimming warnings | Build logs validation |

**Coverage Target:** 95%+ of model loading, option parsing, and async paths.

---

### 2.2 Quantization Support (Enhancement to Existing Feature)

**Feature Goal:** Load INT8/QDQ quantized models; fallback to full-precision if unavailable.

#### Test Categories

**A. Unit Tests: Quantization API Validation**

| Test ID | Scenario | Expected Result | Notes |
|---------|----------|-----------------|-------|
| QNT-U-001 | Parse `PreferQuantized=true` option | `IsQuantizedRequested=true` | Verify enum → bool logic |
| QNT-U-002 | Parse `PreferQuantized=false` (default) | `IsQuantizedRequested=false` | Explicit vs. implicit default |
| QNT-U-003 | Model selection logic: quantized not found | Fallback to full-precision, log warning | Verify `ModelDownloader.GetModelPath` logic |
| QNT-U-004 | Quantization metadata parsing from ONNX graph | Model dimensions unchanged (384 output) | Verify `OnnxEmbeddingModel.GetOutputShape()` |
| QNT-U-005 | Invalid quantization type (e.g., "fp16_quantized") | Treated as "not found", fallback triggered | Enum validation bounds |

**B. Integration Tests: Model Loading & Accuracy**

| Test ID | Scenario | Expected Result | Notes |
|---------|----------|-----------------|-------|
| QNT-I-001 | Load quantized MiniLM model | Embeddings generated (1024-dim vectors) | Use pre-downloaded quantized model |
| QNT-I-002 | Load quantized vs. full-precision for same text | Both return 1024-dim vectors | Dimension consistency |
| QNT-I-003 | Cosine similarity: quantized vs. full on known pairs | Similarity >0.99 (accuracy baseline) | 100 text pairs, statistical validation |
| QNT-I-004 | Quantized model inference latency | <80% of full-precision latency | Benchmark: measure both, calculate ratio |
| QNT-I-005 | Quantized model memory footprint | <60% of full-precision model size | File size + runtime memory |
| QNT-I-006 | Fallback on missing quantized: error handling | Falls back silently, logs info | Verify no exceptions thrown |
| QNT-I-007 | Batch embedding (100 texts) quantized vs. full | Accuracy >0.99 across batch | Statistical comparison |
| QNT-I-008 | Streaming 10K texts quantized | Completes with O(buffer_size) memory | Verify no OOM |

**C. Error Handling & Edge Cases**

| Test ID | Scenario | Expected Result | Notes |
|---------|----------|-----------------|-------|
| QNT-E-001 | Corrupted quantized model file | Fallback to full-precision | Verify recovery path |
| QNT-E-002 | Missing both quantized and full model | Throw `InvalidOperationException` | Clear error message |
| QNT-E-003 | Quantized model with wrong input size | Throw on inference, not load | ONNX validation at runtime |

**Table-Driven Test Template:**
```csharp
[Theory]
[InlineData("all-minilm-l6-v2", true, 0.99, "quantized")]
[InlineData("all-minilm-l6-v2", false, 0.99, "full-precision")]
[InlineData("e5-small", true, 0.98, "quantized")]
[InlineData("e5-small", false, 0.99, "full-precision")]
public async Task QuantizationAccuracy(
    string modelName, 
    bool preferQuantized,
    double expectedMinSimilarity,
    string variantLabel)
{
    // Arrange: Load both quantized and full models
    // Act: Generate embeddings for same text
    // Assert: Similarity >= expectedMinSimilarity
}
```

**Coverage Target:** 90%+ of quantization enum, model selection, and accuracy paths.

---

### 2.3 OpenTelemetry Integration (New Feature)

**Feature Goal:** Emit structured traces (model load, generation, errors) and metrics (latency, throughput) for observability.

#### Test Categories

**A. Unit Tests: Event Generation**

| Test ID | Scenario | Expected Result | Notes |
|---------|----------|-----------------|-------|
| OTEL-U-001 | Generate span on `GenerateAsync` call | ActivitySource emits "generate.embeddings" span | Verify span name, attributes |
| OTEL-U-002 | Span attributes: model name, vector count, batch size | All attributes present | Use `Activity.Current.TagObjects` |
| OTEL-U-003 | Emit metric: "embedding.generation.latency" | Meter exports histogram with duration_ms | Verify histogram buckets |
| OTEL-U-004 | Emit metric: "embedding.generation.count" | Counter increments by input count | Verify counter delta |
| OTEL-U-005 | Span tags: error scenario | Span includes exception, Status=Error | Catch exception, verify span state |
| OTEL-U-006 | Structured logging on model load | ILogger emits structured log with model name | Verify LogLevel=Information |
| OTEL-U-007 | Metric: "oml.model.load.duration_ms" | Histogram recorded on successful load | Baseline: measure vs. expected range |

**B. Integration Tests: Export to Mock Exporter**

| Test ID | Scenario | Expected Result | Notes |
|---------|----------|-----------------|-------|
| OTEL-I-001 | Export spans to memory exporter | 1 span captured per GenerateAsync call | Use `SimpleActivityExporter` |
| OTEL-I-002 | Export metrics to memory exporter | Latency + count metrics exported | Use `InMemoryMetricReader` |
| OTEL-I-003 | Batch generation (100 texts) telemetry | Single span, count metric = 100 | Aggregation validation |
| OTEL-I-004 | Streaming generation (1K texts) telemetry | Multiple spans (per batch), total count = 1K | Verify batch boundaries in traces |
| OTEL-I-005 | Cancellation token usage | Span marked with cancellation status | Verify cancellation handling |
| OTEL-I-006 | Error propagation in telemetry | Exception logged in span events | Use `Activity.AddEvent(new ActivityEvent(...))` |
| OTEL-I-007 | Nested spans (model load → generation) | Parent-child relationship verified | Check `ParentId` in spans |

**C. Performance & Overhead Tests**

| Test ID | Scenario | Expected Result | Notes |
|---------|----------|-----------------|-------|
| OTEL-P-001 | Telemetry disabled (no listener) | No overhead vs. baseline | Measure: <1% latency delta |
| OTEL-P-002 | Telemetry enabled + sampled | <5% latency overhead | Measure: E2E latency with sampler |
| OTEL-P-003 | Telemetry enabled + sampled + export | <10% latency overhead | Measure: latency with export |
| OTEL-P-004 | Memory overhead of span/metric objects | <10MB per 10K spans in memory | Profile heap, GC behavior |

**D. Structured Logging Tests**

| Test ID | Scenario | Expected Result | Notes |
|---------|----------|-----------------|-------|
| OTEL-L-001 | Log structure: model name, version, format | All required fields present | Parse JSON, validate schema |
| OTEL-L-002 | Log level escalation on error | Error spans → LogLevel=Error | Verify hierarchy |
| OTEL-L-003 | Log performance metrics in structured format | Template-based logging with duration_ms | Verify serilog/default pattern |

**Table-Driven Test Template:**
```csharp
[Theory]
[InlineData("model_load", "all-minilm-l6-v2", LogLevel.Information)]
[InlineData("embedding_generate", "batch=32", LogLevel.Information)]
[InlineData("embedding_error", "model_not_found", LogLevel.Error)]
public async Task TelemetryEvent(
    string eventType,
    string eventAttribute,
    LogLevel expectedLevel)
{
    // Arrange: Set up listener
    // Act: Trigger event
    // Assert: Verify span/metric emitted with correct level
}
```

**Coverage Target:** 85%+ of telemetry paths (span creation, metric emission, error handling).

---

### 2.4 Streaming Embeddings API (Phase 1 → Phase 2 Expansion)

**Feature Goal:** Expand streaming to handle 100K+ vectors with memory-safe backpressure and cancellation.

#### Test Categories

**A. Unit Tests: Buffer & Batch Management**

| Test ID | Scenario | Expected Result | Notes |
|---------|----------|-----------------|-------|
| STR-U-001 | Parse `StreamingEmbeddingOptions.BufferSize=32` | Buffer size is 32 | Verify default |
| STR-U-002 | Parse `StreamingEmbeddingOptions.BufferSize=128` | Buffer size is 128 | Custom size |
| STR-U-003 | Invalid buffer size (0, negative) | Throw `ArgumentException` | Input validation |
| STR-U-004 | Buffer timeout option (Phase 2) | `BufferTimeoutMs` parsed correctly | Future-proof test |

**B. Integration Tests: Large-Scale Streaming**

| Test ID | Scenario | Expected Result | Notes |
|---------|----------|-----------------|-------|
| STR-I-001 | Stream 1K vectors, buffer=32 | 32 batches (31 + 1 remainder) | Batch counting |
| STR-I-002 | Stream 100K vectors, buffer=64 | Memory usage O(64 + model) ~100MB | Peak memory profile |
| STR-I-003 | Stream 500K vectors | Completes without OOM | Stress test |
| STR-I-004 | Streaming progress callback | Progress incremented per batch | Verify `IProgress<>` updates |
| STR-I-005 | Streaming yields in order | Embedding order preserved | Validate sequence |
| STR-I-006 | Empty stream | Completes, yields nothing | Edge case |
| STR-I-007 | Single text stream | Yields 1 embedding | Trivial case |

**C. Cancellation & Cleanup**

| Test ID | Scenario | Expected Result | Notes |
|---------|----------|-----------------|-------|
| STR-C-001 | Cancel mid-stream (during batch 5/20) | Current batch completes, stream stops | `OperationCanceledException` thrown |
| STR-C-002 | Cancel during model load | Stream never starts | Cancellation propagated |
| STR-C-003 | Resources freed after cancellation | No leaked sessions/memory | Verify `IAsyncDisposable` called |
| STR-C-004 | Dispose stream early | Remaining items not yielded | Verify enumeration stops |

**D. Memory Validation**

| Test ID | Scenario | Expected Result | Notes |
|---------|----------|-----------------|-------|
| STR-M-001 | Streaming 100K: peak memory vs. batch API | Streaming <150MB, batch >400MB | Ratio >2.5x improvement |
| STR-M-002 | No in-memory list accumulation | Memory constant across iterations | GC heap profile test |
| STR-M-003 | Buffer reuse (no new allocations per batch) | <N allocations for N batches | GC allocation count |

**E. Error Handling & Edge Cases**

| Test ID | Scenario | Expected Result | Notes |
|---------|----------|-----------------|-------|
| STR-E-001 | Model load failure mid-stream | Exception thrown, stream stops cleanly | First batch fails |
| STR-E-002 | Input text too long | Item skipped or truncated (per policy) | ONNX token limit |
| STR-E-003 | Timeout on slow consumer (Phase 2) | Buffer flushes after timeout | Future feature test |

**Table-Driven Test Template:**
```csharp
public static readonly TheoryData<int, int, long> StreamingScenarios =
    new()
    {
        { 100, 32, 100_000_000 },      // vectors, buffer, max_memory_bytes
        { 10000, 64, 200_000_000 },
        { 100000, 128, 150_000_000 },
        { 1000000, 256, 300_000_000 },
    };

[Theory]
[MemberData(nameof(StreamingScenarios))]
public async Task StreamingMemoryProfile(int vectorCount, int bufferSize, long maxMemoryBytes)
{
    // Arrange: Create stream
    // Act: Enumerate and measure peak memory
    // Assert: Memory usage <= maxMemoryBytes
}
```

**Coverage Target:** 90%+ of streaming core logic (buffering, iteration, cleanup).

---

## 3. Test Matrix: Features × Edge Cases

| Feature | Unit Tests | Integration Tests | E2E/Smoke | Performance | Coverage Target |
|---------|------------|-------------------|-----------|-------------|-----------------|
| **AOT** | 5 | 4 | 4 | 2 | 95% |
| **Quantization** | 5 | 8 | 0 | 2 | 90% |
| **OpenTelemetry** | 7 | 7 | 0 | 4 | 85% |
| **Streaming** | 4 | 10 | 0 | 3 | 90% |
| **TOTAL** | **21** | **29** | **4** | **11** | **~88%** |

---

## 4. Test Data Requirements

### 4.1 Models Required

| Model | Purpose | Size | Format | Acquisition |
|-------|---------|------|--------|-------------|
| `all-minilm-l6-v2` (full) | Baseline full-precision | ~34 MB | ONNX | Pre-downloaded in CI cache |
| `all-minilm-l6-v2` (INT8) | Quantization tests | ~10 MB | ONNX QDQ | Pre-downloaded in CI cache |
| `e5-small` (full) | Alternative model tests | ~30 MB | ONNX | Pre-downloaded in CI cache |
| `e5-small` (INT8) | Quantization accuracy validation | ~9 MB | ONNX QDQ | Pre-downloaded in CI cache |

**CI Caching Strategy:** 
- Store models in `.github/models/` or S3 bucket
- Git LFS for binary models (avoid bloating repo)
- Download on first test run, cache for subsequent runs

### 4.2 Test Data Sets

| Dataset | Purpose | Size | Format |
|---------|---------|------|--------|
| **Semantic Pairs** (100) | Accuracy baseline (cosine similarity) | 100 pairs | CSV: (text1, text2, expected_similarity) |
| **Batch Texts** (1K) | Streaming memory tests | 1K texts | Line-delimited JSON |
| **Large Batch** (100K) | Stress test | 100K texts | Streamed (not in-memory) |
| **Edge Cases** | Null/empty/unicode handling | 50 samples | Mixed formats |

**Storage:** In `tests/test-data/` with `.gitignore` for generated outputs.

---

## 5. Test Project Structure (Phase 2 Additions)

```
tests/
├── ElBruno.LocalEmbeddings.Tests/
│   ├── AOT/
│   │   ├── NativeAotPublishTests.cs               (E2E smoke test)
│   │   ├── TrimSafetyTests.cs                    (AOT unit tests)
│   │   └── AotSerializationTests.cs              (JSON serialization)
│   ├── Quantization/
│   │   ├── QuantizedModelLoadingTests.cs         (integration)
│   │   ├── QuantizationAccuracyTests.cs          (accuracy validation)
│   │   └── QuantizationOptionsTests.cs           (unit)
│   ├── OpenTelemetry/
│   │   ├── TelemetrySpanTests.cs                 (span generation)
│   │   ├── TelemetryMetricsTests.cs              (metric export)
│   │   ├── TelemetryPerformanceTests.cs          (overhead)
│   │   └── StructuredLoggingTests.cs             (log structure)
│   ├── Streaming/
│   │   ├── StreamingBufferTests.cs               (buffer management)
│   │   ├── StreamingLargeScaleTests.cs           (100K+ vectors)
│   │   ├── StreamingMemoryTests.cs               (memory validation)
│   │   ├── StreamingCancellationTests.cs         (cleanup)
│   │   └── StreamingEdgeCasesTests.cs            (error handling)
│   └── test-data/
│       ├── semantic-pairs.csv
│       ├── batch-texts-1k.jsonl
│       └── edge-cases.json
├── ElBruno.LocalEmbeddings.Azure.Tests/          (existing)
└── Integration/
    └── Phase2IntegrationTests.cs                 (cross-feature tests)
```

---

## 6. Effort Estimation & Timeline

### Effort Breakdown (in story points)

| Category | Unit Tests | Integration | E2E/Perf | Effort |
|----------|------------|-------------|----------|--------|
| **AOT** | 3 | 5 | 8 | **16** |
| **Quantization** | 3 | 8 | 5 | **16** |
| **OpenTelemetry** | 5 | 8 | 5 | **18** |
| **Streaming** | 3 | 8 | 8 | **19** |
| **Infrastructure** (CI/data) | — | — | — | **8** |
| **TOTAL** | **14** | **29** | **26** | **77** |

### Timeline Estimate (assuming 1 dev + Lambert testing in parallel)

| Phase | Duration | Deliverable |
|-------|----------|------------|
| Week 1-2 | CI setup + test data prep | Models cached, test datasets ready |
| Week 2-3 | AOT + Quantization tests | 32 tests (unit + integration) |
| Week 3-4 | OpenTelemetry tests | 14 tests (spans + metrics) |
| Week 4-5 | Streaming expansion | 17 tests (buffer + E2E) |
| Week 5-6 | Performance baselines + UAT | Benchmarks locked, regressions detected |

**Total:** ~6 weeks for full Phase 2 test suite.

---

## 7. Coverage Targets & Success Criteria

### Code Coverage

- **Unit Tests:** 95%+ line coverage on AOT, quantization options, streaming buffer logic
- **Integration Tests:** 90%+ branch coverage on model loading, fallback paths, cancellation
- **Overall Target:** 88% line coverage for Phase 2 new code

### Performance Baselines (Regression Detection)

| Metric | Baseline | Alert Threshold |
|--------|----------|-----------------|
| Embedding latency (100 texts) | <500ms | >550ms (+10%) |
| Memory usage (100K streaming) | <150MB | >165MB (+10%) |
| OTEL overhead (enabled + export) | <10% | >15% |
| AOT cold start | <500ms | >600ms |
| Model load time | <5s | >6s |

**Tracking:** Store baselines in `.github/workflows/performance-baseline.json`.

### Test Maintenance

- **Flakiness Target:** <2% (max 1 flaky test per 50)
- **Retry Policy:** Transient failures retried once (network, timing)
- **Stability:** Green builds for 10 consecutive commits before release

---

## 8. Phase 1 → Phase 2 Test Migration

### Tests to Enhance
| Phase 1 Test | Phase 2 Enhancement |
|--------------|-------------------|
| `HybridAzureEmbeddingGeneratorTests` | Add AOT serialization for Azure options |
| `ServiceCollectionExtensionsTests` | Add tests for quantization + OTEL DI |
| Streaming skeleton tests (if any) | Expand to full 100K scenario tests |

### New Test Project Files Required
- **No breaking changes** to Phase 1 tests
- Phase 2 tests live in separate directories (clean separation)
- Shared utilities: `test-data/`, fixtures, mock factories

---

## 9. Table-Driven Test Template (Reusable)

```csharp
using ElBruno.LocalEmbeddings.Tests.Fixtures;
using Xunit;

namespace ElBruno.LocalEmbeddings.Tests.FeatureName;

public class FeatureTheoryTests
{
    public static readonly TheoryData<string, int, bool> TestCases = new()
    {
        { "scenario1", 100, true },
        { "scenario2", 1000, false },
        { "scenario3", 10000, true },
    };

    [Theory]
    [MemberData(nameof(TestCases))]
    public async Task Feature_WithVariation_ProducesExpectedResult(
        string scenario,
        int inputSize,
        bool shouldSucceed)
    {
        // Arrange: Setup test-specific data
        var options = new FeatureOptions { /* ... */ };
        var sut = new FeatureClass(options);

        // Act: Execute feature
        var result = shouldSucceed
            ? await sut.ExecuteAsync(inputSize)
            : await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.ExecuteAsync(inputSize));

        // Assert: Verify expected outcome
        if (shouldSucceed)
        {
            Assert.NotNull(result);
            Assert.Equal(inputSize, result.Count);
        }
    }
}
```

---

## 10. Known Limitations & Phase 3 Planning

### Phase 2 Deferred Items
| Item | Reason | Phase 3 Target |
|------|--------|----------------|
| Multi-GPU testing | Requires specialized hardware | Q1 2027 |
| Distributed streaming | Complex infrastructure | Q1 2027 |
| Chaos engineering (fault injection) | Build complexity | Q2 2027 |
| Live production telemetry export (Jaeger/DataDog) | External service dependency | Post-Q3 |

### Phase 3 Test Expansion
- **Hardware-specific tests:** GPU fallback, WASM compatibility
- **Load testing:** 1M+ vectors with backpressure
- **Distributed scenarios:** Multi-instance aggregation
- **Compliance testing:** GDPR, SOC2 audit trails

---

## 11. CI/CD Integration

### GitHub Actions Workflow Updates

**New Workflow Files:**
- `.github/workflows/phase2-aot.yml` — AOT publish + cold start benchmarks
- `.github/workflows/phase2-quantization.yml` — Quantized model tests
- `.github/workflows/phase2-telemetry.yml` — OpenTelemetry export validation
- `.github/workflows/phase2-streaming.yml` — Large-scale streaming stress tests

**Existing Workflow Updates:**
- `test.yml` — Add Phase 2 test suites to matrix
- `performance-baseline.yml` — Lock performance baselines on main branch

### Test Execution Strategy
```yaml
# Parallel execution (reduce CI time)
- Unit tests (all features): ~5 min
- Integration tests (all features): ~15 min
- Performance baselines: ~10 min
- AOT E2E (sequential): ~20 min
- Total: ~40 min (vs. 90 min if serial)
```

---

## 12. Success Metrics

By end of Phase 2, validate:

✅ **Test Coverage:** 88%+ line coverage, 0 untested public APIs  
✅ **Zero Regressions:** All Phase 1 tests pass; new tests detect breaking changes  
✅ **Performance:** Baselines established; overhead <10% for all features  
✅ **Stability:** <2% flake rate, repeatable results  
✅ **Documentation:** Every test method has clear purpose + expected behavior  
✅ **Maintainability:** Table-driven tests reduce maintenance overhead by 50%

---

## Appendix A: Test Naming Convention

All Phase 2 tests follow pattern: `[Feature]_[Scenario]_[ExpectationOrCondition]`

**Examples:**
- `AOT_PublishTrimmed_CompilationSucceeds`
- `Quantization_AccuracySimilarity_ReturnsGreaterThan099`
- `OpenTelemetry_GenerateAsync_EmitsSpanWithModelNameAttribute`
- `Streaming_CancelMidStream_ResourcesFreedWithoutLeaks`

---

## Appendix B: Performance Baseline Template

```json
{
  "phase": "Phase 2",
  "date": "2026-05-19",
  "environment": "Windows 11, .NET 8.0, i7-12700K",
  "metrics": {
    "embedding_latency_100_texts_ms": 420,
    "streaming_memory_100k_vectors_mb": 145,
    "otel_overhead_percent": 8.5,
    "aot_cold_start_ms": 480,
    "model_load_time_s": 4.2
  },
  "notes": "Baseline established pre-optimization phase"
}
```

---

## Next Steps

1. **Week 1:** Review with Architecture team (Ash) for AOT/quantization constraints
2. **Week 1:** Set up CI cache for models and test data
3. **Week 2:** Begin AOT + Quantization test implementation (parallel with Ash's AOT prep)
4. **Week 3:** OpenTelemetry test suite (coordinate with implementation)
5. **Week 4-5:** Streaming expansion + performance baseline collection
6. **Week 6:** UAT validation + Phase 2 release readiness

---

**Document Status:** ✅ Ready for implementation  
**Next Review:** Post-implementation (2026-06-XX)  
**Maintained by:** Lambert (Tester)
