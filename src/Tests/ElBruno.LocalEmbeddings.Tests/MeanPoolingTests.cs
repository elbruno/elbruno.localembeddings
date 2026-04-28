using Microsoft.ML.OnnxRuntime.Tensors;

namespace ElBruno.LocalEmbeddings.Tests;

/// <summary>
/// Tests for PERF-02: ApplyMeanPooling correctness after the scalar-loop → SIMD
/// (TensorPrimitives) refactor, and for PERF-01: ArrayPool flattening regression.
///
/// ApplyMeanPooling is internal (exposed via InternalsVisibleTo) so these tests
/// exercise the actual production implementation directly.
/// </summary>
public class MeanPoolingTests
{
    // -------------------------------------------------------------------------
    // Single-token edge cases
    // -------------------------------------------------------------------------

    [Fact]
    public void MeanPooling_SingleToken_ReturnsTokenEmbedding()
    {
        // batch=1, seq=1, hidden=3 — mean of a single attended token == the token itself
        var data = new float[] { 1.0f, 2.0f, 3.0f };
        var tensor = new DenseTensor<float>(data, [1, 1, 3]);
        var masks = new long[][] { [1L] };

        var result = OnnxEmbeddingModel.ApplyMeanPooling(tensor, masks, batchSize: 1, sequenceLength: 1);

        Assert.Single(result);
        Assert.Equal(3, result[0].Length);
        Assert.Equal(1.0f, result[0][0]);
        Assert.Equal(2.0f, result[0][1]);
        Assert.Equal(3.0f, result[0][2]);
    }

    // -------------------------------------------------------------------------
    // Multi-token averaging
    // -------------------------------------------------------------------------

    [Fact]
    public void MeanPooling_TwoTokensAllAttended_ReturnsCorrectAverage()
    {
        // batch=1, seq=2, hidden=2
        // token0=[1,2], token1=[3,4]  →  mean=[2,3]
        var data = new float[] { 1.0f, 2.0f, 3.0f, 4.0f };
        var tensor = new DenseTensor<float>(data, [1, 2, 2]);
        var masks = new long[][] { [1L, 1L] };

        var result = OnnxEmbeddingModel.ApplyMeanPooling(tensor, masks, batchSize: 1, sequenceLength: 2);

        Assert.Single(result);
        Assert.Equal(2.0f, result[0][0]);
        Assert.Equal(3.0f, result[0][1]);
    }

    [Fact]
    public void MeanPooling_ThreeTokensAllAttended_ReturnsCorrectAverage()
    {
        // batch=1, seq=3, hidden=2
        // tokens: [1,0], [3,0], [5,0]  →  mean=[3,0]
        var data = new float[] { 1.0f, 0.0f, 3.0f, 0.0f, 5.0f, 0.0f };
        var tensor = new DenseTensor<float>(data, [1, 3, 2]);
        var masks = new long[][] { [1L, 1L, 1L] };

        var result = OnnxEmbeddingModel.ApplyMeanPooling(tensor, masks, batchSize: 1, sequenceLength: 3);

        Assert.Single(result);
        Assert.Equal(3.0f, result[0][0]);
        Assert.Equal(0.0f, result[0][1]);
    }

    // -------------------------------------------------------------------------
    // Masked tokens (padding) must be excluded
    // -------------------------------------------------------------------------

    [Fact]
    public void MeanPooling_WithMaskedTokens_IgnoresMaskedPositions()
    {
        // batch=1, seq=3, hidden=2
        // token0=[1,0] mask=1, token1=[3,0] mask=1, token2=[100,100] mask=0 (padding)
        // Expected mean over attended tokens only: [2,0]
        var data = new float[] { 1.0f, 0.0f, 3.0f, 0.0f, 100.0f, 100.0f };
        var tensor = new DenseTensor<float>(data, [1, 3, 2]);
        var masks = new long[][] { [1L, 1L, 0L] };

        var result = OnnxEmbeddingModel.ApplyMeanPooling(tensor, masks, batchSize: 1, sequenceLength: 3);

        Assert.Single(result);
        Assert.Equal(2.0f, result[0][0]);
        Assert.Equal(0.0f, result[0][1]);
    }

    [Fact]
    public void MeanPooling_OnlyFirstTokenAttended_ReturnsFirstTokenEmbedding()
    {
        // All but first token are masked; result must equal token0
        var data = new float[] { 7.0f, 8.0f, 99.0f, 99.0f, 99.0f, 99.0f };
        var tensor = new DenseTensor<float>(data, [1, 3, 2]);
        var masks = new long[][] { [1L, 0L, 0L] };

        var result = OnnxEmbeddingModel.ApplyMeanPooling(tensor, masks, batchSize: 1, sequenceLength: 3);

        Assert.Equal(7.0f, result[0][0]);
        Assert.Equal(8.0f, result[0][1]);
    }

    // -------------------------------------------------------------------------
    // All-masked edge case — must return zero vector, not throw
    // -------------------------------------------------------------------------

    [Fact]
    public void MeanPooling_AllMasked_ReturnsZeroVector()
    {
        var data = new float[] { 1.0f, 2.0f, 3.0f, 4.0f };
        var tensor = new DenseTensor<float>(data, [1, 2, 2]);
        var masks = new long[][] { [0L, 0L] };

        var result = OnnxEmbeddingModel.ApplyMeanPooling(tensor, masks, batchSize: 1, sequenceLength: 2);

        Assert.Single(result);
        Assert.Equal(0.0f, result[0][0]);
        Assert.Equal(0.0f, result[0][1]);
    }

    // -------------------------------------------------------------------------
    // Batch dimension
    // -------------------------------------------------------------------------

    [Fact]
    public void MeanPooling_BatchOf2_EachBatchComputedIndependently()
    {
        // batch=2, seq=2, hidden=2
        // batch0: tokens [1,2],[3,4] all attended → mean [2,3]
        // batch1: tokens [10,20],[30,40] only first attended → [10,20]
        var data = new float[]
        {
            1.0f, 2.0f,    // batch0, seq0
            3.0f, 4.0f,    // batch0, seq1
            10.0f, 20.0f,  // batch1, seq0
            30.0f, 40.0f   // batch1, seq1
        };
        var tensor = new DenseTensor<float>(data, [2, 2, 2]);
        var masks = new long[][]
        {
            [1L, 1L],
            [1L, 0L]
        };

        var result = OnnxEmbeddingModel.ApplyMeanPooling(tensor, masks, batchSize: 2, sequenceLength: 2);

        Assert.Equal(2, result.Length);

        Assert.Equal(2.0f, result[0][0]);
        Assert.Equal(3.0f, result[0][1]);

        Assert.Equal(10.0f, result[1][0]);
        Assert.Equal(20.0f, result[1][1]);
    }

    [Fact]
    public void MeanPooling_BatchOf3_AllIndependentlyCorrect()
    {
        // batch=3, seq=1, hidden=2 — trivial single-token per item
        var data = new float[]
        {
            5.0f, 6.0f,    // batch0
            7.0f, 8.0f,    // batch1
            9.0f, 10.0f    // batch2
        };
        var tensor = new DenseTensor<float>(data, [3, 1, 2]);
        var masks = new long[][] { [1L], [1L], [1L] };

        var result = OnnxEmbeddingModel.ApplyMeanPooling(tensor, masks, batchSize: 3, sequenceLength: 1);

        Assert.Equal(3, result.Length);
        Assert.Equal(5.0f, result[0][0]);
        Assert.Equal(7.0f, result[1][0]);
        Assert.Equal(9.0f, result[2][0]);
    }

    // -------------------------------------------------------------------------
    // Numerical parity: SIMD result must match scalar reference
    // -------------------------------------------------------------------------

    [Fact]
    public void MeanPooling_SimdResult_MatchesScalarReference()
    {
        // Generate non-trivial data to stress-test SIMD vs scalar equivalence.
        // Uses deterministic values so the test is repeatable.
        const int batchSize = 4;
        const int seqLen = 8;
        const int hiddenSize = 16;

        var rng = new Random(42);
        var data = new float[batchSize * seqLen * hiddenSize];
        for (int i = 0; i < data.Length; i++)
            data[i] = (float)(rng.NextDouble() * 2.0 - 1.0);

        var tensor = new DenseTensor<float>(data, [batchSize, seqLen, hiddenSize]);

        // Alternating mask pattern: first half of sequence attended, second half not
        var masks = new long[batchSize][];
        for (int b = 0; b < batchSize; b++)
        {
            masks[b] = new long[seqLen];
            for (int s = 0; s < seqLen; s++)
                masks[b][s] = s < seqLen / 2 ? 1L : 0L;
        }

        // Production result (SIMD path after PERF-02)
        var simdResult = OnnxEmbeddingModel.ApplyMeanPooling(tensor, masks, batchSize, seqLen);

        // Scalar reference implementation
        var scalarResult = ScalarMeanPoolingReference(data, masks, batchSize, seqLen, hiddenSize);

        for (int b = 0; b < batchSize; b++)
        {
            for (int h = 0; h < hiddenSize; h++)
            {
                Assert.Equal(scalarResult[b][h], simdResult[b][h], precision: 4);
            }
        }
    }

    /// <summary>
    /// Reference scalar mean pooling used to verify SIMD output parity (PERF-02).
    /// This intentionally mirrors the pre-PERF-02 implementation.
    /// </summary>
    private static float[][] ScalarMeanPoolingReference(
        float[] data,
        long[][] attentionMasks,
        int batchSize,
        int sequenceLength,
        int hiddenSize)
    {
        var embeddings = new float[batchSize][];

        for (int batch = 0; batch < batchSize; batch++)
        {
            var embedding = new float[hiddenSize];
            long tokenCount = 0;

            for (int seq = 0; seq < sequenceLength; seq++)
            {
                var mask = attentionMasks[batch][seq];
                if (mask == 0) continue;

                tokenCount += mask;
                int offset = (batch * sequenceLength + seq) * hiddenSize;
                for (int h = 0; h < hiddenSize; h++)
                    embedding[h] += data[offset + h] * mask;
            }

            if (tokenCount > 0)
                for (int h = 0; h < hiddenSize; h++)
                    embedding[h] /= tokenCount;

            embeddings[batch] = embedding;
        }

        return embeddings;
    }
}
