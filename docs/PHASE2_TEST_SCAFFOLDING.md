# Phase 2 Test Project Structure & Scaffolding

**Purpose:** Scaffolding template for Phase 2 test directories, fixtures, and shared utilities

---

## Directory Structure (To Create)

```
tests/ElBruno.LocalEmbeddings.Tests/
├── AOT/
│   ├── NativeAotPublishTests.cs          # E2E: publish, cold start, memory
│   ├── TrimSafetyTests.cs               # Unit: no reflection validation
│   ├── AotSerializationTests.cs         # Unit: JSON serialization
│   └── AotFixture.cs                    # Shared fixture for AOT tests
├── Quantization/
│   ├── QuantizedModelLoadingTests.cs     # Integration: model loading
│   ├── QuantizationAccuracyTests.cs      # Integration: similarity >0.99
│   ├── QuantizationOptionsTests.cs       # Unit: enum/option parsing
│   ├── QuantizationErrorHandlingTests.cs # Unit: fallback logic
│   └── QuantizationFixture.cs            # Shared fixture
├── OpenTelemetry/
│   ├── TelemetrySpanTests.cs             # Unit: span emission
│   ├── TelemetryMetricsTests.cs          # Unit: metric emission
│   ├── TelemetryExportTests.cs           # Integration: export validation
│   ├── TelemetryPerformanceTests.cs      # Performance: overhead <5%
│   ├── StructuredLoggingTests.cs         # Unit: log structure
│   └── TelemetryFixture.cs               # Shared fixture
├── Streaming/
│   ├── StreamingBufferTests.cs           # Unit: buffer management
│   ├── StreamingLargeScaleTests.cs       # Integration: 100K+ vectors
│   ├── StreamingMemoryTests.cs           # Integration: O(buffer) memory
│   ├── StreamingCancellationTests.cs     # Integration: cleanup
│   ├── StreamingEdgeCasesTests.cs        # Unit: null, empty, timeout
│   └── StreamingFixture.cs               # Shared fixture
├── Fixtures/
│   ├── ModelFixture.cs                   # Shared: model caching
│   ├── TelemetryFixture.cs               # Shared: OTEL listeners
│   ├── TestDataFixture.cs                # Shared: semantic pairs, batches
│   └── PerformanceFixture.cs             # Shared: memory/latency profiling
├── Helpers/
│   ├── CosineSimilarityCalculator.cs     # Shared: similarity scoring
│   ├── MemoryProfiler.cs                 # Shared: peak memory measurement
│   ├── PerformanceAssertions.cs          # Shared: baseline comparisons
│   └── TestModelManager.cs               # Shared: model download/cache
├── test-data/
│   ├── semantic-pairs.csv                # 100 pairs: (text1, text2, similarity)
│   ├── batch-texts-1k.jsonl             # 1K line-delimited texts
│   ├── edge-cases.json                   # 50 edge case samples
│   ├── quantization-benchmarks.json      # Accuracy baselines
│   └── README.md                         # Data source documentation
└── Integration/
    └── Phase2CrossFeatureTests.cs        # Tests combining AOT+Quantization+OTEL
```

---

## File Templates

### 1. Feature Test File Template

```csharp
// File: tests/ElBruno.LocalEmbeddings.Tests/Quantization/QuantizationAccuracyTests.cs

using System.Collections.Generic;
using ElBruno.LocalEmbeddings.Options;
using ElBruno.LocalEmbeddings.Tests.Fixtures;
using ElBruno.LocalEmbeddings.Tests.Helpers;
using Xunit;

namespace ElBruno.LocalEmbeddings.Tests.Quantization;

/// <summary>
/// Integration tests for quantized model accuracy validation.
/// Ensures INT8 quantized models maintain >0.99 cosine similarity
/// with full-precision baselines across semantic pairs.
/// </summary>
public class QuantizationAccuracyTests : IAsyncLifetime
{
    private readonly ModelFixture _modelFixture;
    private readonly TestDataFixture _testDataFixture;

    public QuantizationAccuracyTests()
    {
        _modelFixture = new ModelFixture();
        _testDataFixture = new TestDataFixture();
    }

    public async Task InitializeAsync()
    {
        await _modelFixture.InitializeAsync();
        await _testDataFixture.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _modelFixture.DisposeAsync();
        await _testDataFixture.DisposeAsync();
    }

    public static readonly TheoryData<string, bool, double> AccuracyScenarios = new()
    {
        // Model, IsQuantized, MinAcceptableSimilarity
        { "all-minilm-l6-v2", true, 0.99 },
        { "all-minilm-l6-v2", false, 0.99 },
        { "e5-small", true, 0.98 },
        { "e5-small", false, 0.99 },
    };

    /// <summary>
    /// QNT-I-003: Validate cosine similarity between quantized and full-precision
    /// models on known semantic pairs remains above threshold.
    /// </summary>
    [Theory]
    [MemberData(nameof(AccuracyScenarios))]
    public async Task GenerateAsync_QuantizedVsFullPrecision_SimilarityAboveThreshold(
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

        var testPairs = await _testDataFixture.LoadSemanticPairsAsync();

        // Act & Assert
        var similarities = new List<float>();
        foreach (var (text1, text2, _) in testPairs)
        {
            var emb1 = await generator.GenerateAsync(new[] { text1 });
            var emb2 = await generator.GenerateAsync(new[] { text2 });

            var similarity = CosineSimilarityCalculator.Calculate(
                emb1[0].Vector,
                emb2[0].Vector);

            similarities.Add(similarity);

            Assert.True(similarity >= minSimilarity,
                $"Similarity {similarity:F4} below threshold {minSimilarity:F4} " +
                $"for pair: '{text1}' vs '{text2}'");
        }

        // Verify statistical consistency
        var avgSimilarity = similarities.Average();
        var minActual = similarities.Min();
        Assert.True(avgSimilarity >= minSimilarity,
            $"Average similarity {avgSimilarity:F4} below threshold");
        Assert.True(minActual >= minSimilarity * 0.95,
            $"Minimum similarity {minActual:F4} below 95% of threshold");
    }

    /// <summary>
    /// QNT-I-004: Benchmark quantized model latency against full-precision.
    /// Quantized should be <80% of full-precision latency.
    /// </summary>
    [Fact]
    public async Task GenerateAsync_QuantizedLatency_LessThan80PercentOfFull()
    {
        // Arrange
        const string modelName = "all-minilm-l6-v2";
        const int iterationCount = 50;  // Warm-up + measurements
        const int warmUpCount = 5;      // Skip first N iterations

        var optionsFull = new LocalEmbeddingsOptions { Model = modelName, PreferQuantized = false };
        var optionsQuantized = new LocalEmbeddingsOptions { Model = modelName, PreferQuantized = true };

        using var generatorFull = new OnnxEmbeddingModel(optionsFull);
        using var generatorQuantized = new OnnxEmbeddingModel(optionsQuantized);

        await generatorFull.LoadAsync();
        await generatorQuantized.LoadAsync();

        var testTexts = Enumerable.Range(0, iterationCount)
            .Select(i => $"Sample text {i}")
            .ToList();

        // Act
        var latenciesFull = new List<long>();
        var latenciesQuantized = new List<long>();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        foreach (var text in testTexts)
        {
            await generatorFull.GenerateAsync(new[] { text });
        }
        sw.Stop();
        // Skip warmup, collect measurements

        foreach (var text in testTexts.Skip(warmUpCount))
        {
            sw.Restart();
            await generatorFull.GenerateAsync(new[] { text });
            sw.Stop();
            latenciesFull.Add(sw.ElapsedMilliseconds);
        }

        foreach (var text in testTexts.Skip(warmUpCount))
        {
            sw.Restart();
            await generatorQuantized.GenerateAsync(new[] { text });
            sw.Stop();
            latenciesQuantized.Add(sw.ElapsedMilliseconds);
        }

        // Assert
        var avgLatencyFull = latenciesFull.Average();
        var avgLatencyQuantized = latenciesQuantized.Average();
        var ratio = avgLatencyQuantized / avgLatencyFull;

        Assert.True(ratio < 0.80,
            $"Quantized latency {avgLatencyQuantized:F2}ms is {ratio:P1} of full " +
            $"({avgLatencyFull:F2}ms); should be <80%");
    }
}
```

### 2. Shared Fixture Template

```csharp
// File: tests/ElBruno.LocalEmbeddings.Tests/Fixtures/ModelFixture.cs

using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace ElBruno.LocalEmbeddings.Tests.Fixtures;

/// <summary>
/// Manages model caching and lifecycle for integration tests.
/// Handles download, validation, and cleanup of ONNX models.
/// </summary>
public class ModelFixture : IAsyncLifetime
{
    private readonly string _modelCachePath;
    private readonly IList<string> _loadedModels = new List<string>();

    public ModelFixture()
    {
        _modelCachePath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            ".github",
            "models");
    }

    public async Task InitializeAsync()
    {
        // Ensure model cache directory exists
        Directory.CreateDirectory(_modelCachePath);

        // Pre-download required models if not present
        await EnsureModelAsync("all-minilm-l6-v2.onnx");
        await EnsureModelAsync("all-minilm-l6-v2-int8.onnx");
        await EnsureModelAsync("e5-small.onnx");
        await EnsureModelAsync("e5-small-int8.onnx");
    }

    public async Task DisposeAsync()
    {
        // Cleanup if needed (typically models are cached for reuse)
        await Task.CompletedTask;
    }

    private async Task EnsureModelAsync(string modelFileName)
    {
        var modelPath = Path.Combine(_modelCachePath, modelFileName);

        if (File.Exists(modelPath))
        {
            return;  // Already cached
        }

        // Download from HuggingFace or S3 bucket
        // Implementation: fetch from remote source
        await Task.Delay(0);  // Placeholder
    }

    public string GetModelPath(string modelName, bool quantized = false)
    {
        var suffix = quantized ? "-int8" : "";
        var fileName = $"{modelName}{suffix}.onnx";
        var fullPath = Path.Combine(_modelCachePath, fileName);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Model not found at {fullPath}. " +
                $"Ensure InitializeAsync was called.");
        }

        return fullPath;
    }
}
```

### 3. Shared Helper Template

```csharp
// File: tests/ElBruno.LocalEmbeddings.Tests/Helpers/CosineSimilarityCalculator.cs

using System;

namespace ElBruno.LocalEmbeddings.Tests.Helpers;

/// <summary>
/// Utility for computing cosine similarity between embedding vectors.
/// </summary>
public static class CosineSimilarityCalculator
{
    /// <summary>
    /// Compute cosine similarity: (a · b) / (||a|| * ||b||)
    /// </summary>
    public static float Calculate(float[] vectorA, float[] vectorB)
    {
        if (vectorA == null) throw new ArgumentNullException(nameof(vectorA));
        if (vectorB == null) throw new ArgumentNullException(nameof(vectorB));
        if (vectorA.Length != vectorB.Length)
            throw new ArgumentException("Vectors must have same dimension");

        float dotProduct = 0;
        float normA = 0;
        float normB = 0;

        for (int i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            normA += vectorA[i] * vectorA[i];
            normB += vectorB[i] * vectorB[i];
        }

        normA = (float)Math.Sqrt(normA);
        normB = (float)Math.Sqrt(normB);

        if (normA == 0 || normB == 0)
            throw new ArgumentException("Vectors cannot have zero norm");

        return dotProduct / (normA * normB);
    }
}
```

### 4. Test Data Fixture Template

```csharp
// File: tests/ElBruno.LocalEmbeddings.Tests/Fixtures/TestDataFixture.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace ElBruno.LocalEmbeddings.Tests.Fixtures;

/// <summary>
/// Loads and caches test data (semantic pairs, batch texts, edge cases)
/// from test-data/ directory.
/// </summary>
public class TestDataFixture : IAsyncLifetime
{
    private readonly string _testDataPath;
    private List<(string text1, string text2, double expectedSimilarity)> _semanticPairs =
        new();
    private List<string> _batchTexts = new();
    private List<string> _edgeCases = new();

    public TestDataFixture()
    {
        _testDataPath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "test-data");
    }

    public async Task InitializeAsync()
    {
        await LoadSemanticPairsAsync();
        await LoadBatchTextsAsync();
        await LoadEdgeCasesAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    private async Task LoadSemanticPairsAsync()
    {
        var filePath = Path.Combine(_testDataPath, "semantic-pairs.csv");
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Test data not found: {filePath}");

        var lines = await File.ReadAllLinesAsync(filePath);
        foreach (var line in lines.Skip(1))  // Skip header
        {
            var parts = line.Split(',');
            if (parts.Length >= 3 &&
                double.TryParse(parts[2], out var similarity))
            {
                _semanticPairs.Add((parts[0], parts[1], similarity));
            }
        }
    }

    private async Task LoadBatchTextsAsync()
    {
        var filePath = Path.Combine(_testDataPath, "batch-texts-1k.jsonl");
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Test data not found: {filePath}");

        var lines = await File.ReadAllLinesAsync(filePath);
        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                var doc = JsonDocument.Parse(line);
                if (doc.RootElement.TryGetProperty("text", out var textElement))
                {
                    _batchTexts.Add(textElement.GetString());
                }
            }
        }
    }

    private async Task LoadEdgeCasesAsync()
    {
        var filePath = Path.Combine(_testDataPath, "edge-cases.json");
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Test data not found: {filePath}");

        var json = await File.ReadAllTextAsync(filePath);
        var doc = JsonDocument.Parse(json);
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            if (element.TryGetProperty("text", out var textElement))
            {
                _edgeCases.Add(textElement.GetString());
            }
        }
    }

    public Task<IEnumerable<(string, string, double)>> LoadSemanticPairsAsync()
    {
        return Task.FromResult((IEnumerable<(string, string, double)>)_semanticPairs);
    }

    public Task<IEnumerable<string>> LoadBatchTextsAsync()
    {
        return Task.FromResult((IEnumerable<string>)_batchTexts);
    }

    public Task<IEnumerable<string>> LoadEdgeCasesAsync()
    {
        return Task.FromResult((IEnumerable<string>)_edgeCases);
    }
}
```

---

## Test Data Files (CSV/JSON Templates)

### semantic-pairs.csv
```csv
text1,text2,expected_similarity
"Hello world","Hello world",0.99
"The cat sat on the mat","A feline rested on the carpet",0.85
"I love programming","I enjoy coding",0.92
"The weather is nice today","The sun is shining brightly",0.78
"This is a completely different topic","Unrelated sentence",0.15
```

### batch-texts-1k.jsonl
```json
{"id": 1, "text": "Sample text 1"}
{"id": 2, "text": "Sample text 2"}
{"id": 3, "text": "A longer text with multiple words and concepts that should generate meaningful embeddings"}
...
{"id": 1000, "text": "Sample text 1000"}
```

### edge-cases.json
```json
[
  {"id": 1, "text": "", "category": "empty"},
  {"id": 2, "text": " ", "category": "whitespace"},
  {"id": 3, "text": "123456789", "category": "numbers"},
  {"id": 4, "text": "Special !@#$%^&*() chars", "category": "special"},
  {"id": 5, "text": "Emoji test 🚀 🎯", "category": "emoji"},
  {"id": 6, "text": "Very " + "long ".repeat(1000) + "text", "category": "long"}
]
```

---

## MSBuild Integration

### Add to .csproj for test project:

```xml
<ItemGroup>
  <InternalsVisibleTo Include="ElBruno.LocalEmbeddings.Tests" />
</ItemGroup>

<ItemGroup>
  <RuntimeHostConfigurationOption Include="DOTNET_GC_HEAP_COUNT" Value="1" />
  <RuntimeHostConfigurationOption Include="DOTNET_GC_SERVER" Value="false" />
</ItemGroup>

<ItemGroup>
  <None Update="test-data/**/*">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

---

## GitHub Actions Integration

### .github/workflows/phase2-tests.yml

```yaml
name: Phase 2 Tests

on: [push, pull_request]

jobs:
  test:
    strategy:
      matrix:
        suite: [aot, quantization, telemetry, streaming]
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: 8.0.x
      
      - name: Cache ONNX Models
        uses: actions/cache@v3
        with:
          path: .github/models/
          key: onnx-models-${{ matrix.suite }}
      
      - name: Run ${{ matrix.suite }} Tests
        run: |
          dotnet test tests/ElBruno.LocalEmbeddings.Tests/ \
            --filter="Category=${{ matrix.suite }}" \
            --configuration Release \
            --logger="trx;LogFileName=results-${{ matrix.suite }}.trx"
      
      - name: Upload Test Results
        uses: actions/upload-artifact@v3
        if: always()
        with:
          name: test-results-${{ matrix.suite }}
          path: '**/results-*.trx'
```

---

## Next Steps for Implementation

1. ✅ Create directory structure above
2. ✅ Add fixture and helper files (use templates)
3. ✅ Create test data files (CSV, JSON, JSONL)
4. ✅ Implement test classes (use feature templates)
5. ✅ Configure GitHub Actions workflows
6. ✅ Run first batch of tests locally
7. ✅ Lock performance baselines (Week 6)

---

**Document Status:** ✅ Template Ready  
**Last Updated:** 2026-05-19  
**Maintained by:** Lambert
