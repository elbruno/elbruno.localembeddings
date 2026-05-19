using System.Collections.Generic;
using System.Threading.Tasks;
using ElBruno.LocalEmbeddings.Options;
using ElBruno.LocalEmbeddings.Tests.Phase2.Helpers;

namespace ElBruno.LocalEmbeddings.Tests.Phase2.Fixtures;

/// <summary>
/// Quantization Test Fixture (extends TestDataFixture for Phase 2 Week 2).
/// 
/// Provides test data and helpers for quantization accuracy and performance testing.
/// Includes Float32 baseline variants and quantized model variants (if available).
/// </summary>
public class QuantizationTestFixture : IAsyncLifetime
{
    private readonly TestDataFixture _baseFixture;
    private readonly Dictionary<string, byte[]> _quantizedModelVariants;
    private readonly Dictionary<string, (double accuracy, long latencyMs, long memorySizeBytes)> _baselineMetrics;
    private LocalEmbeddingsOptions? _float32Options;
    private LocalEmbeddingsOptions? _int8Options;
    private LocalEmbeddingsOptions? _int4Options;
    private LocalEmbeddingsOptions? _float16Options;

    public QuantizationTestFixture()
    {
        _baseFixture = new TestDataFixture();
        _quantizedModelVariants = new Dictionary<string, byte[]>();
        _baselineMetrics = new Dictionary<string, (double, long, long)>();
    }

    /// <summary>
    /// Gets the test data directory from base fixture.
    /// </summary>
    public string TestDataDirectory => _baseFixture.GetTestDataDirectory();

    /// <summary>
    /// Initialize fixture with baseline data and quantized model variants.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _baseFixture.InitializeAsync();
        
        // Create baseline options (Float32)
        _float32Options = new LocalEmbeddingsOptions
        {
            ModelName = "sentence-transformers/all-MiniLM-L6-v2",
            CacheDirectory = TestDataDirectory,
            EnsureModelDownloaded = false,
            PreferQuantized = false
        };

        // Create quantized variants (if models available, use registry; otherwise skip gracefully)
        _int8Options = new LocalEmbeddingsOptions
        {
            ModelName = "sentence-transformers/all-MiniLM-L6-v2-int8",
            CacheDirectory = TestDataDirectory,
            EnsureModelDownloaded = false,
            PreferQuantized = true
        };

        _int4Options = new LocalEmbeddingsOptions
        {
            ModelName = "sentence-transformers/all-MiniLM-L6-v2-int4",
            CacheDirectory = TestDataDirectory,
            EnsureModelDownloaded = false,
            PreferQuantized = true
        };

        _float16Options = new LocalEmbeddingsOptions
        {
            ModelName = "sentence-transformers/all-MiniLM-L6-v2-float16",
            CacheDirectory = TestDataDirectory,
            EnsureModelDownloaded = false,
            PreferQuantized = true
        };

        // Initialize baseline metrics (from performance-baseline.json or defaults)
        InitializeBaselineMetrics();

        await Task.CompletedTask;
    }

    /// <summary>
    /// Dispose the fixture and clean up resources.
    /// </summary>
    public async Task DisposeAsync()
    {
        await _baseFixture.DisposeAsync();
    }

    /// <summary>
    /// Get Float32 baseline options for comparison.
    /// </summary>
    public LocalEmbeddingsOptions GetFloat32Options()
    {
        return _float32Options ?? throw new InvalidOperationException("Fixture not initialized");
    }

    /// <summary>
    /// Get Int8 quantized options.
    /// </summary>
    public LocalEmbeddingsOptions GetInt8Options()
    {
        return _int8Options ?? throw new InvalidOperationException("Fixture not initialized");
    }

    /// <summary>
    /// Get Int4 quantized options.
    /// </summary>
    public LocalEmbeddingsOptions GetInt4Options()
    {
        return _int4Options ?? throw new InvalidOperationException("Fixture not initialized");
    }

    /// <summary>
    /// Get Float16 quantized options.
    /// </summary>
    public LocalEmbeddingsOptions GetFloat16Options()
    {
        return _float16Options ?? throw new InvalidOperationException("Fixture not initialized");
    }

    /// <summary>
    /// Register a quantized model variant (for testing unavailable variants).
    /// </summary>
    public void RegisterQuantizedVariant(string name, byte[] modelData)
    {
        _quantizedModelVariants[name] = modelData;
    }

    /// <summary>
    /// Get a registered quantized model variant, or return null if unavailable.
    /// </summary>
    public byte[]? GetQuantizedVariant(string name)
    {
        return _quantizedModelVariants.TryGetValue(name, out var data) ? data : null;
    }

    /// <summary>
    /// Check if a quantized variant is available in registry.
    /// </summary>
    public bool HasQuantizedVariant(string name)
    {
        return _quantizedModelVariants.ContainsKey(name);
    }

    /// <summary>
    /// Get baseline accuracy for a quantization variant (reference for accuracy tests).
    /// 
    /// Returns: (accuracy: 0.0-1.0, latencyMs, memorySizeBytes)
    /// </summary>
    public (double accuracy, long latencyMs, long memorySizeBytes) GetBaselineMetrics(string quantizationType)
    {
        if (_baselineMetrics.TryGetValue(quantizationType, out var metrics))
        {
            return metrics;
        }

        // Return defaults if not registered
        return quantizationType switch
        {
            "Float32" => (1.0, 100, 100_000_000),        // 100 MB baseline
            "Float16" => (0.999, 50, 50_000_000),        // 50% speedup, 50% memory
            "Int8" => (0.99, 40, 30_000_000),            // 60% speedup, 70% memory
            "Int4" => (0.97, 30, 15_000_000),            // 70% speedup, 85% memory
            _ => (0.99, 100, 100_000_000)
        };
    }

    /// <summary>
    /// Register baseline metrics for a quantization variant.
    /// </summary>
    public void RegisterBaselineMetrics(string quantizationType, double accuracy, long latencyMs, long memorySizeBytes)
    {
        _baselineMetrics[quantizationType] = (accuracy, latencyMs, memorySizeBytes);
    }

    /// <summary>
    /// Get semantic test pairs for accuracy validation (uses base TestDataFixture).
    /// </summary>
    public IEnumerable<(string text1, string text2, double expectedSimilarity)> GetSemanticTestPairs()
    {
        return EmbeddingDataFactory.GenerateSemanticPairs();
    }

    /// <summary>
    /// Get edge case texts for robustness testing (uses base TestDataFixture).
    /// </summary>
    public IEnumerable<string> GetEdgeCaseTexts()
    {
        return EmbeddingDataFactory.GenerateEdgeCaseTexts();
    }

    /// <summary>
    /// Get batch texts for bulk accuracy testing.
    /// </summary>
    public IEnumerable<string> GetBatchTexts(int count = 32)
    {
        return EmbeddingDataFactory.GenerateBatchTexts(count);
    }

    /// <summary>
    /// Initialize baseline metrics from default values or configuration.
    /// Can be extended to load from performance-baseline.json if needed.
    /// </summary>
    private void InitializeBaselineMetrics()
    {
        // Default baseline metrics (updated from actual measurements in Week 3)
        RegisterBaselineMetrics("Float32", accuracy: 1.0, latencyMs: 100, memorySizeBytes: 100_000_000);
        RegisterBaselineMetrics("Float16", accuracy: 0.999, latencyMs: 55, memorySizeBytes: 50_000_000);
        RegisterBaselineMetrics("Int8", accuracy: 0.99, latencyMs: 45, memorySizeBytes: 30_000_000);
        RegisterBaselineMetrics("Int4", accuracy: 0.97, latencyMs: 35, memorySizeBytes: 15_000_000);
    }
}
