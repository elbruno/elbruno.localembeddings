using BenchmarkDotNet.Attributes;
using System.Numerics.Tensors;

namespace ElBruno.LocalEmbeddings.Benchmarks;

/// <summary>
/// Isolated mean pooling benchmarks using synthetic data and TensorPrimitives SIMD operations.
/// No ONNX session required.
/// </summary>
[MemoryDiagnoser]
public class MeanPoolingBenchmarks
{
    private const int HiddenSize = 768;

    private float[] _tensorData128 = null!;
    private long[] _mask128 = null!;
    private float[] _tensorData512 = null!;
    private long[] _mask512 = null!;

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(42);

        _tensorData128 = new float[128 * HiddenSize];
        for (int i = 0; i < _tensorData128.Length; i++)
            _tensorData128[i] = (float)(rng.NextDouble() * 2.0 - 1.0);

        _mask128 = new long[128];
        for (int i = 0; i < _mask128.Length; i++) _mask128[i] = 1;

        _tensorData512 = new float[512 * HiddenSize];
        for (int i = 0; i < _tensorData512.Length; i++)
            _tensorData512[i] = (float)(rng.NextDouble() * 2.0 - 1.0);

        _mask512 = new long[512];
        for (int i = 0; i < _mask512.Length; i++) _mask512[i] = 1;
    }

    [Benchmark]
    public float[] MeanPooling_128Tokens_768Hidden()
        => MeanPool(_tensorData128, _mask128, 128, HiddenSize);

    [Benchmark]
    public float[] MeanPooling_512Tokens_768Hidden()
        => MeanPool(_tensorData512, _mask512, 512, HiddenSize);

    private static float[] MeanPool(float[] tensorData, long[] mask, int seqLength, int hiddenSize)
    {
        var embedding = new float[hiddenSize];
        int tokenCount = 0;

        for (int seq = 0; seq < seqLength; seq++)
        {
            if (mask[seq] == 0) continue;
            tokenCount++;
            int offset = seq * hiddenSize;
            TensorPrimitives.Add(embedding, tensorData.AsSpan(offset, hiddenSize), embedding);
        }

        if (tokenCount > 0)
            TensorPrimitives.Divide(embedding, (float)tokenCount, embedding);

        return embedding;
    }
}
