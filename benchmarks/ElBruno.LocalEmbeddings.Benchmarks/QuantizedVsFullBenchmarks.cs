using BenchmarkDotNet.Attributes;
using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Options;

namespace ElBruno.LocalEmbeddings.Benchmarks;

/// <summary>
/// Full-precision vs quantized model throughput comparison.
/// Requires both model.onnx and model_quantized.onnx / model_int8.onnx to be present for meaningful results.
/// Gracefully skips when model files are unavailable.
/// </summary>
[MemoryDiagnoser]
public class QuantizedVsFullBenchmarks
{
    private LocalEmbeddingGenerator? _fullGenerator;
    private LocalEmbeddingGenerator? _quantizedGenerator;

    [GlobalSetup]
    public void Setup()
    {
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
    }

    /// <summary>Inference with the full-precision (FP32) model.</summary>
    [Benchmark]
    public async Task FullPrecision_Embedding()
    {
        if (_fullGenerator is null) return;
        await _fullGenerator.GenerateAsync(["The quick brown fox jumps over the lazy dog."]).ConfigureAwait(false);
    }

    /// <summary>Inference with the quantized (INT8) model when available; falls back to FP32.</summary>
    [Benchmark]
    public async Task Quantized_Embedding()
    {
        if (_quantizedGenerator is null) return;
        await _quantizedGenerator.GenerateAsync(["The quick brown fox jumps over the lazy dog."]).ConfigureAwait(false);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _fullGenerator?.Dispose();
        _quantizedGenerator?.Dispose();
    }
}
