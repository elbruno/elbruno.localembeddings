using BenchmarkDotNet.Attributes;
using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Options;

namespace ElBruno.LocalEmbeddings.Benchmarks;

/// <summary>Cold start model loading benchmarks.</summary>
[MemoryDiagnoser]
public class ModelLoadingBenchmarks
{
    private string? _modelDirectory;

    [GlobalSetup]
    public void Setup() => _modelDirectory = BenchmarkHelpers.TryResolveModelDirectory();

    /// <summary>Creates a new generator and times full initialization from disk.</summary>
    [Benchmark]
    public Task LoadModel_ColdStart()
    {
        if (_modelDirectory is null) return Task.CompletedTask;
        using var generator = new LocalEmbeddingGenerator(new LocalEmbeddingsOptions
        {
            ModelPath = _modelDirectory,
            EnsureModelDownloaded = false,
        });
        return Task.CompletedTask;
    }

    /// <summary>Model already cached by the OS; re-initializes the generator from warm disk cache.</summary>
    [Benchmark]
    public Task LoadModel_WarmCache()
    {
        if (_modelDirectory is null) return Task.CompletedTask;
        using var generator = new LocalEmbeddingGenerator(new LocalEmbeddingsOptions
        {
            ModelPath = _modelDirectory,
            EnsureModelDownloaded = false,
        });
        return Task.CompletedTask;
    }
}
