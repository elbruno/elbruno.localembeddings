using BenchmarkDotNet.Attributes;
using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Options;
using Microsoft.Extensions.AI;

namespace BenchmarkSample;

/// <summary>
/// Benchmarks for embedding generation throughput and memory allocation.
/// </summary>
[MemoryDiagnoser]
public class EmbeddingBenchmarks
{
    private LocalEmbeddingGenerator _generator = null!;
    private string[] _batchTexts = null!;

    [GlobalSetup]
    public void Setup()
    {
        _generator = new LocalEmbeddingGenerator(new LocalEmbeddingsOptions
        {
            ModelName = "sentence-transformers/all-MiniLM-L6-v2",
            EnsureModelDownloaded = true
        });

        _batchTexts = Enumerable.Range(0, 100)
            .Select(i => $"Sample sentence number {i} for benchmarking local embeddings generation.")
            .ToArray();
    }

    [Benchmark(Baseline = true, Description = "Single text embedding")]
    public Task<GeneratedEmbeddings<Embedding<float>>> SingleEmbedding()
        => _generator.GenerateAsync(["The quick brown fox jumps over the lazy dog."]);

    [Benchmark(Description = "Batch embedding")]
    [Arguments(10)]
    [Arguments(50)]
    [Arguments(100)]
    public Task<GeneratedEmbeddings<Embedding<float>>> BatchEmbedding(int count)
        => _generator.GenerateAsync(_batchTexts.Take(count));

    [GlobalCleanup]
    public void Cleanup() => _generator.Dispose();
}
