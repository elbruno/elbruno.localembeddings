using BenchmarkDotNet.Attributes;
using ElBruno.LocalEmbeddings.Harrier;
using ElBruno.LocalEmbeddings.Harrier.Options;

namespace ElBruno.LocalEmbeddings.Benchmarks;

/// <summary>End-to-end Harrier embedding generation throughput benchmarks.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class HarrierEmbeddingBenchmarks
{
    private HarrierEmbeddingGenerator? _generator;
    private string[] _texts10 = [];
    private string[] _texts100 = [];

    [GlobalSetup]
    public void Setup()
    {
        _texts10 = Enumerable.Range(0, 10)
            .Select(i => $"Sample sentence number {i} for Harrier embedding benchmarks.")
            .ToArray();
        _texts100 = Enumerable.Range(0, 100)
            .Select(i => $"Sample sentence number {i} for Harrier embedding benchmarks.")
            .ToArray();

        var modelDir = BenchmarkHelpers.TryResolveHarrierModelDirectory();
        if (modelDir is null) return;

        try
        {
            _generator = HarrierEmbeddingGenerator.CreateAsync(new HarrierEmbeddingsOptions
            {
                ModelPath = modelDir,
                EnsureModelDownloaded = false,
            }).GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            _generator = null;
        }
    }

    [Benchmark]
    public async Task SingleEmbedding()
    {
        if (_generator is null) return;
        await _generator.GenerateAsync(["The quick brown fox jumps over the lazy dog."]).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task Batch10()
    {
        if (_generator is null) return;
        await _generator.GenerateAsync(_texts10).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task Batch100()
    {
        if (_generator is null) return;
        await _generator.GenerateAsync(_texts100).ConfigureAwait(false);
    }

    [GlobalCleanup]
    public void Cleanup() => _generator?.Dispose();
}
