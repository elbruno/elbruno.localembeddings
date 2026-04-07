using BenchmarkDotNet.Attributes;
using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Harrier;
using ElBruno.LocalEmbeddings.Harrier.Options;
using ElBruno.LocalEmbeddings.Options;

namespace ElBruno.LocalEmbeddings.Benchmarks;

/// <summary>Head-to-head comparison: base library (MiniLM) vs Harrier single-embedding latency.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class HarrierVsBaseBenchmarks
{
    private LocalEmbeddingGenerator? _baseGenerator;
    private HarrierEmbeddingGenerator? _harrierGenerator;

    [GlobalSetup]
    public void Setup()
    {
        var baseModelDir = BenchmarkHelpers.TryResolveModelDirectory();
        if (baseModelDir is not null)
        {
            try
            {
                _baseGenerator = new LocalEmbeddingGenerator(new LocalEmbeddingsOptions
                {
                    ModelPath = baseModelDir,
                    EnsureModelDownloaded = false,
                });
            }
            catch (Exception)
            {
                _baseGenerator = null;
            }
        }

        var harrierModelDir = BenchmarkHelpers.TryResolveHarrierModelDirectory();
        if (harrierModelDir is not null)
        {
            try
            {
                _harrierGenerator = HarrierEmbeddingGenerator.CreateAsync(new HarrierEmbeddingsOptions
                {
                    ModelPath = harrierModelDir,
                    EnsureModelDownloaded = false,
                }).GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                _harrierGenerator = null;
            }
        }
    }

    [Benchmark(Baseline = true)]
    public async Task BaseLibrarySingleEmbed()
    {
        if (_baseGenerator is null) return;
        await _baseGenerator.GenerateAsync(["The quick brown fox jumps over the lazy dog."]).ConfigureAwait(false);
    }

    [Benchmark]
    public async Task HarrierSingleEmbed()
    {
        if (_harrierGenerator is null) return;
        await _harrierGenerator.GenerateAsync(["The quick brown fox jumps over the lazy dog."]).ConfigureAwait(false);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _baseGenerator?.Dispose();
        _harrierGenerator?.Dispose();
    }
}
