using BenchmarkDotNet.Attributes;
using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Options;

namespace ElBruno.LocalEmbeddings.Benchmarks;

/// <summary>Per-item throughput comparison: 10 individual calls vs one batched call.</summary>
[MemoryDiagnoser]
public class SingleVsBatchBenchmarks
{
    private LocalEmbeddingGenerator? _generator;
    private string[] _texts = [];

    [GlobalSetup]
    public void Setup()
    {
        _texts = Enumerable.Range(0, 10)
            .Select(i => $"Sample sentence number {i} for throughput benchmarking.")
            .ToArray();

        var modelDir = BenchmarkHelpers.TryResolveModelDirectory();
        if (modelDir is null) return;

        try
        {
            _generator = new LocalEmbeddingGenerator(new LocalEmbeddingsOptions
            {
                ModelPath = modelDir,
                EnsureModelDownloaded = false,
            });
        }
        catch (Exception)
        {
            _generator = null;
        }
    }

    /// <summary>Ten individual GenerateAsync calls, one per text item.</summary>
    [Benchmark]
    public async Task SingleText_10Times()
    {
        if (_generator is null) return;
        for (int i = 0; i < _texts.Length; i++)
            await _generator.GenerateAsync([_texts[i]]).ConfigureAwait(false);
    }

    /// <summary>One batched GenerateAsync call with all 10 items.</summary>
    [Benchmark]
    public async Task BatchText_10Items()
    {
        if (_generator is null) return;
        await _generator.GenerateAsync(_texts).ConfigureAwait(false);
    }

    [GlobalCleanup]
    public void Cleanup() => _generator?.Dispose();
}
