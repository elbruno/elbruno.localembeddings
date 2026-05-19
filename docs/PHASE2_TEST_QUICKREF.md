# Phase 2 Test Implementation Quick Reference

**For:** Implementation team (Ash, Kane, others)  
**Purpose:** At-a-glance test checklist, patterns, and entry points

---

## Test Checklist by Feature

### ✅ Native AOT Tests (Start Here)

**Must-Have Tests (7):**
- [ ] AOT-U-001 → Check: No reflection in `OnnxEmbeddingModel.Load`
- [ ] AOT-U-004 → Check: `LocalEmbeddingsOptions` JSON serializability
- [ ] AOT-I-001 → E2E: Can we load model in trimmed runtime?
- [ ] AOT-E2E-001 → Publish as self-contained, measure binary size
- [ ] AOT-E2E-002 → Cold start latency <500ms
- [ ] AOT-E2E-003 → Memory footprint <200MB
- [ ] AOT-E2E-004 → Zero trimming warnings in build log

**File Location:** `tests/ElBruno.LocalEmbeddings.Tests/AOT/`

---

### ✅ Quantization Tests (17 total)

**Unit Tests (5):**
- [ ] QNT-U-001 → `PreferQuantized=true` → bool conversion
- [ ] QNT-U-003 → Missing quantized model → fallback logic
- [ ] QNT-U-004 → ONNX graph metadata parsing
- [ ] QNT-U-005 → Invalid quantization type → enum bounds

**Integration Tests (8):**
- [ ] QNT-I-001 → Load quantized MiniLM model
- [ ] QNT-I-003 → **CRITICAL:** Cosine similarity >0.99 between quantized/full
- [ ] QNT-I-004 → Latency ratio: quantized <80% of full
- [ ] QNT-I-005 → Memory ratio: quantized <60% of full
- [ ] QNT-I-006 → Fallback on missing quantized (no exception)
- [ ] QNT-I-008 → Streaming 10K texts quantized (no OOM)

**File Location:** `tests/ElBruno.LocalEmbeddings.Tests/Quantization/`

---

### ✅ OpenTelemetry Tests (14 total)

**Unit Tests (7):**
- [ ] OTEL-U-001 → Generate span on `GenerateAsync`
- [ ] OTEL-U-002 → Span attributes: model, vector count, batch size
- [ ] OTEL-U-003 → Metric: "embedding.generation.latency" histogram
- [ ] OTEL-U-004 → Metric: "embedding.generation.count" counter
- [ ] OTEL-U-006 → Structured logging on model load

**Integration Tests (7):**
- [ ] OTEL-I-001 → Export spans to memory exporter
- [ ] OTEL-I-002 → Export metrics to memory exporter
- [ ] OTEL-I-004 → Streaming 1K texts → multiple spans, total count=1K
- [ ] OTEL-P-002 → Telemetry overhead <5% latency
- [ ] OTEL-L-001 → Log structure validation (required fields)

**File Location:** `tests/ElBruno.LocalEmbeddings.Tests/OpenTelemetry/`

---

### ✅ Streaming Tests (17 total)

**Unit Tests (4):**
- [ ] STR-U-001 → `BufferSize=32` default
- [ ] STR-U-003 → Invalid buffer size (0, negative) → exception
- [ ] STR-I-001 → Stream 1K vectors, buffer=32 → 32 batches
- [ ] STR-I-002 → **CRITICAL:** Stream 100K vectors → O(64 + model) memory

**Integration Tests (10):**
- [ ] STR-I-005 → Embedding order preserved
- [ ] STR-I-006 → Empty stream → no yields
- [ ] STR-C-001 → Cancel mid-stream → current batch completes
- [ ] STR-C-003 → Resources freed after cancellation (no leaks)
- [ ] STR-M-001 → **CRITICAL:** Streaming 100K <150MB vs. batch >400MB
- [ ] STR-E-001 → Model load failure → stops cleanly

**File Location:** `tests/ElBruno.LocalEmbeddings.Tests/Streaming/`

---

## Test Data Setup

### Models (Pre-cache in CI)

```bash
# Download to .github/models/ or S3 bucket
all-minilm-l6-v2 (full)      → ~34 MB
all-minilm-l6-v2 (INT8)      → ~10 MB
e5-small (full)              → ~30 MB
e5-small (INT8)              → ~9 MB
```

**CI Download Script:**
```bash
# .github/workflows/setup-test-data.yml
- name: Cache Models
  uses: actions/cache@v3
  with:
    path: .github/models/
    key: onnx-models-${{ hashFiles('.github/models/*.json') }}
```

### Test Data Files

**Location:** `tests/test-data/`

```
semantic-pairs.csv              (100 pairs with similarity)
batch-texts-1k.jsonl           (1K line-delimited JSON)
edge-cases.json                (50 edge case samples)
quantization-benchmarks.json    (accuracy baseline)
```

---

## Table-Driven Test Skeleton

Use this for all Phase 2 tests:

```csharp
using Xunit;
using ElBruno.LocalEmbeddings;

namespace ElBruno.LocalEmbeddings.Tests.Quantization;

public class QuantizationAccuracyTests
{
    public static readonly TheoryData<string, bool, double> TestScenarios = new()
    {
        { "all-minilm-l6-v2", true, 0.99 },   // Model, Quantized?, MinSimilarity
        { "all-minilm-l6-v2", false, 0.99 },
        { "e5-small", true, 0.98 },
        { "e5-small", false, 0.99 },
    };

    [Theory]
    [MemberData(nameof(TestScenarios))]
    public async Task Accuracy_QuantizedVsFull_SimilarityAboveThreshold(
        string modelName,
        bool isQuantized,
        double minSimilarity)
    {
        // Arrange
        var options = new LocalEmbeddingsOptions
        {
            Model = modelName,
            PreferQuantized = isQuantized,
        };
        
        using var generator = new OnnxEmbeddingModel(options);
        await generator.LoadAsync();

        // Act
        var result1 = await generator.GenerateAsync(new[] { "Hello world" });
        var result2 = await generator.GenerateAsync(new[] { "Hello world" });

        // Assert
        var similarity = CosineSimilarity(result1[0].Vector, result2[0].Vector);
        Assert.True(similarity >= minSimilarity,
            $"Similarity {similarity} below threshold {minSimilarity}");
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        // Implementation: dot product / (||a|| * ||b||)
    }
}
```

---

## Performance Baseline Locks

### Baseline Check-In (Week 6)

Add to `.github/workflows/performance-baseline.yml`:

```yaml
- name: Lock Performance Baselines
  run: |
    dotnet test --logger="console;verbosity=detailed" \
      --filter="FullyQualifiedName~Performance" \
      --configuration Release
  env:
    PERFORMANCE_BASELINE_LOCK: 'true'
```

### Regression Detection

```csharp
[Fact]
public async Task Streaming_Memory_RegresssionDetection()
{
    // Arrange
    var baselineMemoryMb = 145;  // Week 6 lock-in
    var allowedIncreaseMb = 15;  // 10% tolerance

    // Act
    var actualMemoryMb = await MeasurePeakMemory(/* ... */);

    // Assert
    Assert.True(
        actualMemoryMb <= baselineMemoryMb + allowedIncreaseMb,
        $"Memory regressed: {actualMemoryMb}MB > {baselineMemoryMb + allowedIncreaseMb}MB baseline");
}
```

---

## CI/CD Workflow Matrix

### Parallel Execution Strategy

```yaml
test-matrix:
  strategy:
    matrix:
      test-suite:
        - aot
        - quantization
        - telemetry
        - streaming
  steps:
    - run: dotnet test tests/ElBruno.LocalEmbeddings.Tests/${{ matrix.test-suite }}
```

**Expected Runtimes:**
- AOT: ~20 min (includes publish + cold start)
- Quantization: ~10 min (model loading)
- Telemetry: ~8 min (span + metric export)
- Streaming: ~15 min (100K+ vectors)

---

## Manual Test Checklist (Before Release)

**Required Manual Validation:**
- [ ] Publish as Native AOT on Windows + Linux (manual)
- [ ] Load quantized model on Raspberry Pi (manual)
- [ ] Verify OTEL metrics in Jaeger UI (manual)
- [ ] Stream 100K vectors with monitoring dashboard (manual)

---

## Common Issues & Fixes

| Issue | Root Cause | Fix |
|-------|-----------|-----|
| AOT publish fails with "trimming warnings" | Reflection detected | Add `[DynamicallyAccessedMembers]` |
| Quantization accuracy <0.99 | Model mismatch (full vs INT8) | Verify both models from same source |
| OTEL spans not exported | Listener not registered | Check `ActivitySource.AddActivityListener()` |
| Streaming OOM at 100K | Buffer not releasing | Verify `IAsyncDisposable` disposal |
| Performance regression >10% | Optimization removed | Bisect recent changes |

---

## Test Run Examples

### Run All Phase 2 Tests
```bash
dotnet test tests/ElBruno.LocalEmbeddings.Tests/ \
  --filter="Category=AOT|Category=Quantization|Category=OpenTelemetry|Category=Streaming" \
  --configuration Release \
  --verbosity detailed
```

### Run Specific Feature Tests
```bash
# Quantization only
dotnet test tests/ElBruno.LocalEmbeddings.Tests/Quantization/

# Streaming with memory profiling
dotnet test tests/ElBruno.LocalEmbeddings.Tests/Streaming/ \
  --filter="FullyQualifiedName~Memory" \
  --logger="console;verbosity=detailed"
```

### Run Performance Baselines Only
```bash
dotnet test tests/ElBruno.LocalEmbeddings.Tests/ \
  --filter="Category=Performance" \
  --configuration Release \
  --logger="json;LogFileName=perf-baseline.json"
```

---

## Key Files to Modify

| File | Change |
|------|--------|
| `tests/ElBruno.LocalEmbeddings.Tests/...` | Add feature-specific test classes |
| `.github/workflows/test.yml` | Add Phase 2 test matrix |
| `.github/workflows/performance-baseline.yml` | Lock baselines (Week 6) |
| `.github/models/` | Pre-cache ONNX models |
| `tests/test-data/` | Add semantic pairs, batch texts, edge cases |

---

## Success Criteria (Go/No-Go)

✅ **GO:** If all of:
- 88%+ line coverage on new code
- All 55+ tests passing (unit + integration + E2E)
- Performance baselines locked
- <2% flake rate
- Documentation complete

❌ **NO-GO:** If any of:
- Flake rate >2%
- Performance regression >10%
- Coverage <85%
- Untested public APIs
- CI/CD failures in >1 platform

---

**Last Updated:** 2026-05-19  
**Maintained by:** Lambert  
**Next Sync:** Pre-implementation (Week 1)
