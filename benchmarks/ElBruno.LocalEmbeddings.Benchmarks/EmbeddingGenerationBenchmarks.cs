using BenchmarkDotNet.Attributes;
using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Options;

namespace ElBruno.LocalEmbeddings.Benchmarks;

/// <summary>End-to-end embedding generation throughput benchmarks.</summary>
[MemoryDiagnoser]
public class EmbeddingGenerationBenchmarks
{
    private LocalEmbeddingGenerator? _generator;
    private string[] _texts10 = [];
    private string[] _texts100 = [];

    [GlobalSetup]
    public void Setup()
    {
        _texts10 = Enumerable.Range(0, 10)
            .Select(i => $"Sample sentence number {i} for embedding benchmarks.")
            .ToArray();
        _texts100 = Enumerable.Range(0, 100)
            .Select(i => $"Sample sentence number {i} for embedding benchmarks.")
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

    [Benchmark]
    public async Task GenerateSingleEmbedding()
    {
        if (_generator is null) return;
        await _generator.GenerateAsync(["The quick brown fox jumps over the lazy dog."]).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task GenerateBatch10()
    {
        if (_generator is null) return;
        await _generator.GenerateAsync(_texts10).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task GenerateBatch100()
    {
        if (_generator is null) return;
        await _generator.GenerateAsync(_texts100).ConfigureAwait(false);
    }

    [GlobalCleanup]
    public void Cleanup() => _generator?.Dispose();
}
