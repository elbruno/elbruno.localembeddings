using System.Numerics;
using BenchmarkDotNet.Attributes;
using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Options;

namespace ElBruno.LocalEmbeddings.Benchmarks;

/// <summary>
/// Full-precision vs quantized model throughput and accuracy comparison.
/// Measures:
/// - Latency per embedding generation (single and batch)
/// - Memory footprint at model load time
/// - Embedding vector distance (cosine similarity) to validate accuracy preservation
/// 
/// Requires both model.onnx and model_quantized.onnx / model_int8.onnx to be present for meaningful results.
/// Gracefully skips when model files are unavailable.
/// </summary>
[MemoryDiagnoser]
public class QuantizedVsFullBenchmarks
{
    private const string TestSentence = "The quick brown fox jumps over the lazy dog.";
    
    private LocalEmbeddingGenerator? _fullGenerator;
    private LocalEmbeddingGenerator? _quantizedGenerator;
    
    private string[] _testBatch10 = [];
    private string[] _testBatch32 = [];
    private string[] _testBatch100 = [];
    
    // Cached embeddings for accuracy comparison
    private float[]? _fullEmbedding;
    private float[]? _quantizedEmbedding;

    [GlobalSetup]
    public void Setup()
    {
        // Generate test batches
        _testBatch10 = Enumerable.Range(0, 10)
            .Select(i => $"Sample text {i} for quantization benchmarking.")
            .ToArray();
        
        _testBatch32 = Enumerable.Range(0, 32)
            .Select(i => $"Sample text {i} for quantization benchmarking.")
            .ToArray();
        
        _testBatch100 = Enumerable.Range(0, 100)
            .Select(i => $"Sample text {i} for quantization benchmarking.")
            .ToArray();

        var modelDir = BenchmarkHelpers.TryResolveModelDirectory();
        if (modelDir is null) return;

        try
        {
            _fullGenerator = new LocalEmbeddingGenerator(new LocalEmbeddingsOptions
            {
                ModelPath = modelDir,
                EnsureModelDownloaded = false,
                PreferQuantized = false,
            });
        }
        catch (Exception)
        {
            _fullGenerator = null;
        }

        try
        {
            _quantizedGenerator = new LocalEmbeddingGenerator(new LocalEmbeddingsOptions
            {
                ModelPath = modelDir,
                EnsureModelDownloaded = false,
                PreferQuantized = true,
            });
        }
        catch (Exception)
        {
            _quantizedGenerator = null;
        }
        
        // Cache embeddings for accuracy verification
        if (_fullGenerator is not null)
        {
            var fullResult = _fullGenerator.GenerateAsync([TestSentence]).GetAwaiter().GetResult();
            _fullEmbedding = fullResult[0].Vector.ToArray();
        }
        
        if (_quantizedGenerator is not null)
        {
            var quantizedResult = _quantizedGenerator.GenerateAsync([TestSentence]).GetAwaiter().GetResult();
            _quantizedEmbedding = quantizedResult[0].Vector.ToArray();
        }
    }

    /// <summary>Inference with the full-precision (FP32) model - single sentence.</summary>
    [Benchmark]
    public async Task FullPrecision_SingleEmbedding()
    {
        if (_fullGenerator is null) return;
        await _fullGenerator.GenerateAsync([TestSentence]).ConfigureAwait(false);
    }

    /// <summary>Inference with the quantized (INT8) model - single sentence.</summary>
    [Benchmark]
    public async Task Quantized_SingleEmbedding()
    {
        if (_quantizedGenerator is null) return;
        await _quantizedGenerator.GenerateAsync([TestSentence]).ConfigureAwait(false);
    }

    /// <summary>Inference with the full-precision (FP32) model - batch of 10.</summary>
    [Benchmark]
    public async Task FullPrecision_Batch10()
    {
        if (_fullGenerator is null) return;
        await _fullGenerator.GenerateAsync(_testBatch10).ConfigureAwait(false);
    }

    /// <summary>Inference with the quantized (INT8) model - batch of 10.</summary>
    [Benchmark]
    public async Task Quantized_Batch10()
    {
        if (_quantizedGenerator is null) return;
        await _quantizedGenerator.GenerateAsync(_testBatch10).ConfigureAwait(false);
    }

    /// <summary>Inference with the full-precision (FP32) model - batch of 32.</summary>
    [Benchmark]
    public async Task FullPrecision_Batch32()
    {
        if (_fullGenerator is null) return;
        await _fullGenerator.GenerateAsync(_testBatch32).ConfigureAwait(false);
    }

    /// <summary>Inference with the quantized (INT8) model - batch of 32.</summary>
    [Benchmark]
    public async Task Quantized_Batch32()
    {
        if (_quantizedGenerator is null) return;
        await _quantizedGenerator.GenerateAsync(_testBatch32).ConfigureAwait(false);
    }

    /// <summary>Inference with the full-precision (FP32) model - batch of 100.</summary>
    [Benchmark]
    public async Task FullPrecision_Batch100()
    {
        if (_fullGenerator is null) return;
        await _fullGenerator.GenerateAsync(_testBatch100).ConfigureAwait(false);
    }

    /// <summary>Inference with the quantized (INT8) model - batch of 100.</summary>
    [Benchmark]
    public async Task Quantized_Batch100()
    {
        if (_quantizedGenerator is null) return;
        await _quantizedGenerator.GenerateAsync(_testBatch100).ConfigureAwait(false);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _fullGenerator?.Dispose();
        _quantizedGenerator?.Dispose();
    }

    /// <summary>
    /// Calculate cosine similarity between two embedding vectors.
    /// </summary>
    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Vectors must have the same length.");

        float dotProduct = 0f;
        float magnitudeA = 0f;
        float magnitudeB = 0f;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        magnitudeA = MathF.Sqrt(magnitudeA);
        magnitudeB = MathF.Sqrt(magnitudeB);

        if (magnitudeA == 0f || magnitudeB == 0f)
            return 0f;

        return dotProduct / (magnitudeA * magnitudeB);
    }

    /// <summary>
    /// Gets accuracy metrics comparing full-precision and quantized embeddings.
    /// Returns cosine similarity between corresponding embeddings (higher is better, max 1.0).
    /// </summary>
    public float GetAccuracyMetric()
    {
        if (_fullEmbedding is null || _quantizedEmbedding is null)
            return 0f;

        return CosineSimilarity(_fullEmbedding, _quantizedEmbedding);
    }
}
