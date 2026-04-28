using BenchmarkDotNet.Attributes;
using ElBruno.LocalEmbeddings.Extensions;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalEmbeddings.Benchmarks;

/// <summary>Heap-based nearest-neighbour search benchmarks using synthetic embedding corpus.</summary>
[MemoryDiagnoser]
public class FindClosestBenchmarks
{
    [Params(100, 1000, 10000)]
    public int CorpusSize { get; set; }

    [Params(5, 10, 50)]
    public int TopK { get; set; }

    private List<(Embedding<float> Item, Embedding<float> Embedding)> _corpus = null!;
    private Embedding<float> _query = null!;

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(42);

        _corpus = new List<(Embedding<float>, Embedding<float>)>(CorpusSize);
        for (int i = 0; i < CorpusSize; i++)
        {
            var emb = new Embedding<float>(CreateRandomVector(rng, 384));
            _corpus.Add((emb, emb));
        }

        _query = new Embedding<float>(CreateRandomVector(rng, 384));
    }

    /// <summary>Find top-K closest embeddings using the PriorityQueue min-heap implementation.</summary>
    [Benchmark]
    public List<(Embedding<float>, float)> FindClosest_Heap()
        => _corpus.FindClosest(_query, TopK);

    private static float[] CreateRandomVector(Random rng, int dimensions)
    {
        var vector = new float[dimensions];
        for (int i = 0; i < dimensions; i++)
            vector[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
        return vector;
    }
}
