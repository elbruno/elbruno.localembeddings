using BenchmarkDotNet.Attributes;
using ElBruno.LocalEmbeddings.Extensions;
using Microsoft.Extensions.AI;

namespace BenchmarkSample;

/// <summary>
/// Benchmarks for cosine similarity and nearest-neighbour search.
/// </summary>
[MemoryDiagnoser]
public class SimilarityBenchmarks
{
    private ReadOnlyMemory<float> _vectorA384;
    private ReadOnlyMemory<float> _vectorB384;
    private ReadOnlyMemory<float> _vectorA768;
    private ReadOnlyMemory<float> _vectorB768;

    private List<(string Item, Embedding<float> Embedding)> _corpus100 = null!;
    private List<(string Item, Embedding<float> Embedding)> _corpus1000 = null!;
    private Embedding<float> _queryEmbedding = null!;

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(42);

        _vectorA384 = CreateRandomVector(rng, 384);
        _vectorB384 = CreateRandomVector(rng, 384);
        _vectorA768 = CreateRandomVector(rng, 768);
        _vectorB768 = CreateRandomVector(rng, 768);

        _corpus100 = CreateCorpus(rng, 100, 384);
        _corpus1000 = CreateCorpus(rng, 1000, 384);
        _queryEmbedding = new Embedding<float>(CreateRandomVector(rng, 384));
    }

    [Benchmark(Description = "CosineSimilarity 384-dim")]
    public float CosineSimilarity384()
        => _vectorA384.CosineSimilarity(_vectorB384);

    [Benchmark(Description = "CosineSimilarity 768-dim")]
    public float CosineSimilarity768()
        => _vectorA768.CosineSimilarity(_vectorB768);

    [Benchmark(Description = "FindClosest top-5 in 100")]
    public List<(string, float)> FindClosest100()
        => _corpus100.FindClosest(_queryEmbedding, topK: 5);

    [Benchmark(Description = "FindClosest top-5 in 1000")]
    public List<(string, float)> FindClosest1000()
        => _corpus1000.FindClosest(_queryEmbedding, topK: 5);

    private static float[] CreateRandomVector(Random rng, int dimensions)
    {
        var vector = new float[dimensions];
        for (int i = 0; i < dimensions; i++)
        {
            vector[i] = (float)(rng.NextDouble() * 2 - 1);
        }
        return vector;
    }

    private static List<(string Item, Embedding<float> Embedding)> CreateCorpus(
        Random rng, int count, int dimensions)
    {
        var corpus = new List<(string, Embedding<float>)>(count);
        for (int i = 0; i < count; i++)
        {
            corpus.Add(($"Document {i}", new Embedding<float>(CreateRandomVector(rng, dimensions))));
        }
        return corpus;
    }
}
