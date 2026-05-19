# Phase 2 Test Infrastructure — Developer Quick Reference

## Using the Fixtures

### Basic Template

```csharp
using ElBruno.LocalEmbeddings.Tests.Phase2.Fixtures;
using ElBruno.LocalEmbeddings.Tests.Phase2.Helpers;
using Xunit;

namespace ElBruno.LocalEmbeddings.Tests.Phase2.YourFeature;

public class YourFeatureTests : IAsyncLifetime
{
    private readonly ModelFixture _modelFixture;
    private readonly PerformanceFixture _perfFixture;
    private readonly TelemetryFixture _telemetryFixture;

    public YourFeatureTests()
    {
        _modelFixture = new ModelFixture();
        _perfFixture = new PerformanceFixture();
        _telemetryFixture = new TelemetryFixture();
    }

    public async Task InitializeAsync()
    {
        await _modelFixture.InitializeAsync();
        await _perfFixture.InitializeAsync();
        await _telemetryFixture.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _modelFixture.DisposeAsync();
        await _perfFixture.DisposeAsync();
        await _telemetryFixture.DisposeAsync();
    }

    [Fact]
    public async Task Your_Test_Does_Something()
    {
        // Arrange
        var options = _modelFixture.GetDefaultOptions();
        var testData = EmbeddingDataFactory.GenerateBatchTexts(100);

        // Act
        var latency = await _perfFixture.MeasureAsync(
            "your_operation",
            async () => { /* your test code */ }
        );

        // Assert
        Assert.True(latency < 1000);
    }
}
```

## Common Usage Patterns

### Generate Test Data

```csharp
// Deterministic vectors
var vectors = EmbeddingDataFactory.GenerateTestVectors(100);  // 100 vectors, 384 dims

// Semantic pairs for accuracy testing
var pairs = EmbeddingDataFactory.GenerateSemanticPairs();  // 10 pairs with known similarity

// Batch texts
var texts = EmbeddingDataFactory.GenerateBatchTexts(32);  // 32 sample texts

// Edge cases
var edges = EmbeddingDataFactory.GenerateEdgeCaseTexts();  // Empty, long, special chars, emoji

// Calculate similarity (for validation)
var similarity = EmbeddingDataFactory.CosineSimilarity(vector1, vector2);  // 0.0 to 1.0
```

### Measure Performance

```csharp
// Measure latency of async operation
var (result, elapsedMs) = await _perfFixture.MeasureAsync<string>(
    "operation_name",
    async () => await DoSomething()
);

// Measure memory
var bytes = _perfFixture.MeasureMemoryUsage();

// Record custom metric
_perfFixture.RecordLatency("my_operation", 150);  // 150ms
_perfFixture.RecordMemory("my_operation", 1024 * 1024);  // 1MB
_perfFixture.RecordThroughput("items_per_sec", 10000);

// Validate against baseline
var (isValid, percentDiff, message) = _perfFixture.ValidateMeasurement(
    "latency.generate_batch_100",
    actualLatency,
    isLowerBetter: true
);
Assert.True(isValid, message);  // Fails if >10% worse
```

### Test OpenTelemetry

```csharp
// Get activities emitted
var activities = _telemetryFixture.RecordedActivities;

// Find by operation name
var genActivity = _telemetryFixture.FindActivityByName("generate.embeddings");

// Get tag value
var modelName = _telemetryFixture.GetActivityTag(genActivity, "model.name");

// Record metric
_telemetryFixture.RecordMetric("embedding.generation.count", 100);
_telemetryFixture.IncrementCounter("model.cache.hits");

// Count activities
var count = _telemetryFixture.GetActivityCountByName("generate.embeddings");
```

### Test with Quantization

```csharp
// Get quantized options
var quantOptions = _modelFixture.GetQuantizedOptions(preferQuantized: true);

// Generate all variants for a model
var variants = QuantizationVariantFactory.GenerateVariantsForModel("all-minilm-l6-v2");
// Returns: Float32 (baseline), Float16, Int8

// Get accuracy test scenarios
var scenarios = QuantizationVariantFactory.GenerateAccuracyTestScenarios();

// Get speedup test scenarios
var perfTests = QuantizationVariantFactory.GenerateSpeedupTestScenarios();

// Use in theory test
[Theory]
[MemberData(nameof(GetScenarios))]
public async Task Accuracy_Test(string model, bool preferQuantized, double minSimilarity)
{
    // Test code
}

public static TheoryData<string, bool, double> GetScenarios()
{
    var data = new TheoryData<string, bool, double>();
    foreach (var (model, quantized, format, minSim) in 
        QuantizationVariantFactory.GenerateAccuracyTestScenarios())
    {
        data.Add(model, quantized, minSim);
    }
    return data;
}
```

### Generate Test Data Files

```csharp
private readonly TestDataFixture _dataFixture;

public TestDataFixture _dataFixture { get; } = new();

public async Task InitializeAsync()
{
    await _dataFixture.InitializeAsync();
}

public async Task DisposeAsync()
{
    await _dataFixture.DisposeAsync();
}

[Fact]
public void Can_Read_Semantic_Pairs()
{
    var path = _dataFixture.GetSemanticPairsPath();
    var lines = File.ReadAllLines(path);
    Assert.NotEmpty(lines);
}
```

## File Organization

Place your feature tests here:

```
tests/ElBruno.LocalEmbeddings.Tests/
├── Phase2/
│   ├── Fixtures/                 ← Shared (don't modify)
│   ├── Helpers/                  ← Shared (don't modify)
│   ├── AOT/                       ← Create these folders
│   │   ├── TrimSafetyTests.cs
│   │   └── AotSerializationTests.cs
│   ├── Quantization/
│   │   ├── QuantizedModelLoadingTests.cs
│   │   └── QuantizationAccuracyTests.cs
│   ├── OpenTelemetry/
│   │   ├── TelemetrySpanTests.cs
│   │   └── TelemetryMetricsTests.cs
│   └── Streaming/
│       ├── StreamingLargeScaleTests.cs
│       └── StreamingMemoryTests.cs
```

## Release Gate Checklist

Tests must pass these gates before RC:

- [ ] **AOT-E2E-001:** Cold-start <2 seconds
- [ ] **QNT-I-003:** Quantization accuracy >0.99
- [ ] **STR-M-001:** Streaming 100K vectors <150MB
- [ ] **OTEL-P-002:** Telemetry overhead <2%
- [ ] All 54+ tests pass on .NET 8 & 10
- [ ] Code coverage 88% target met
- [ ] Zero flakiness (rerun 100× = 100% pass)

## Performance Baseline Targets

These are the minimum performance requirements:

| Metric | Target | Gate |
|--------|--------|------|
| AOT cold start | <2 seconds | AOT-E2E-001 |
| Quantization accuracy | >0.99 similarity | QNT-I-003 |
| Streaming 100K memory | <150MB | STR-M-001 |
| Telemetry overhead | <2% latency | OTEL-P-002 |
| Single embedding | 10ms | Baseline |
| Batch 100 | 200ms | Baseline |
| Model load (cold) | 1000ms | Baseline |
| Model load (cached) | 50ms | Baseline |

## Regression Detection

**Automatic on every test run:**
- Latency >10% worse = **FAIL** ❌
- Latency 5-10% worse = **WARN** ⚠️
- Latency 0-5% worse = **OK** ✅

Baseline stored in `performance-baseline.json`. Update targets there before committing.

## Common Issues

### "The name 'EmbeddingDataFactory' does not exist"
→ Add `using ElBruno.LocalEmbeddings.Tests.Phase2.Helpers;`

### "The name 'ModelFixture' does not exist"
→ Add `using ElBruno.LocalEmbeddings.Tests.Phase2.Fixtures;`

### Activity/telemetry not recorded
→ Ensure `TelemetryFixture.InitializeAsync()` is called
→ Check `_telemetryFixture.GetActivityCount()` to verify recording

### Performance test fails on slow machine
→ Increase tolerance in `ValidateMeasurement(...)`
→ Or update baseline in `performance-baseline.json`

---

**Questions?** See `.squad/agents/lambert/phase2-week1-infrastructure.md` for implementation details.
