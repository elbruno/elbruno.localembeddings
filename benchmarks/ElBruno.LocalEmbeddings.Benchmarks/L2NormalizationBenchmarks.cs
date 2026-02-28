using BenchmarkDotNet.Attributes;
using System.Numerics.Tensors;

namespace ElBruno.LocalEmbeddings.Benchmarks;

/// <summary>L2 normalization benchmarks using TensorPrimitives SIMD operations on synthetic data.</summary>
[MemoryDiagnoser]
public class L2NormalizationBenchmarks
{
    private float[] _source768 = null!;
    private float[] _work768 = null!;
    private float[] _source384 = null!;
    private float[] _work384 = null!;

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(42);

        _source768 = new float[768];
        for (int i = 0; i < _source768.Length; i++)
            _source768[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
        _work768 = new float[768];

        _source384 = new float[384];
        for (int i = 0; i < _source384.Length; i++)
            _source384[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
        _work384 = new float[384];
    }

    /// <summary>L2-normalize a 768-dimensional embedding vector (all-MiniLM-L6-v2 output size).</summary>
    [Benchmark]
    public void NormalizeEmbedding_768d()
    {
        _source768.AsSpan().CopyTo(_work768);
        float norm = TensorPrimitives.Norm(_work768.AsSpan());
        if (norm > 0f)
            TensorPrimitives.Divide(_work768, norm, _work768);
    }

    /// <summary>L2-normalize a 384-dimensional embedding vector.</summary>
    [Benchmark]
    public void NormalizeEmbedding_384d()
    {
        _source384.AsSpan().CopyTo(_work384);
        float norm = TensorPrimitives.Norm(_work384.AsSpan());
        if (norm > 0f)
            TensorPrimitives.Divide(_work384, norm, _work384);
    }
}
