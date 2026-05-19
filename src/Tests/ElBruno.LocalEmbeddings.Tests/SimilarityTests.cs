using ElBruno.LocalEmbeddings.Extensions;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalEmbeddings.Tests;

/// <summary>
/// Tests for SIMD-optimized CosineSimilarity function (TensorPrimitives).
/// Verifies correctness against known vectors and edge cases.
/// </summary>
public class SimilarityTests
{
    // =========================================================================
    // Table-driven tests: Known vector pairs with expected similarity
    // =========================================================================

    public static readonly TheoryData<float[], float[], float> KnownVectorPairs = new()
    {
        // Identical vectors → similarity = 1.0
        { new float[] { 1f, 0f, 0f }, new float[] { 1f, 0f, 0f }, 1.0f },
        
        // Orthogonal vectors → similarity = 0.0
        { new float[] { 1f, 0f, 0f }, new float[] { 0f, 1f, 0f }, 0.0f },
        
        // Opposite vectors → similarity = -1.0
        { new float[] { 1f, 0f, 0f }, new float[] { -1f, 0f, 0f }, -1.0f },
        
        // 45-degree angle → similarity = sqrt(2)/2 ≈ 0.707
        { new float[] { 1f, 0f }, new float[] { 1f, 1f }, MathF.Sqrt(2f) / 2f },
        
        // Arbitrary normalized vectors
        { new float[] { 0.6f, 0.8f }, new float[] { 0.8f, 0.6f }, 0.96f },
    };

    [Theory]
    [MemberData(nameof(KnownVectorPairs))]
    public void CosineSimilarity_WithKnownVectors_ReturnsExpectedSimilarity(
        float[] vectorA, float[] vectorB, float expected)
    {
        var a = new ReadOnlyMemory<float>(vectorA);
        var b = new ReadOnlyMemory<float>(vectorB);

        var result = a.CosineSimilarity(b);

        Assert.Equal(expected, result, precision: 4);
    }

    // =========================================================================
    // Edge cases: Zero, NaN, very large, very small values
    // =========================================================================

    [Fact]
    public void CosineSimilarity_WithZeroVector_ReturnsNaN()
    {
        var zero = new ReadOnlyMemory<float>(new float[] { 0f, 0f, 0f });
        var normal = new ReadOnlyMemory<float>(new float[] { 1f, 0f, 0f });

        var result = zero.CosineSimilarity(normal);

        Assert.True(float.IsNaN(result), "Zero vector should produce NaN");
    }

    [Fact]
    public void CosineSimilarity_WithBothZeroVectors_ReturnsNaN()
    {
        var zeroA = new ReadOnlyMemory<float>(new float[] { 0f, 0f, 0f });
        var zeroB = new ReadOnlyMemory<float>(new float[] { 0f, 0f, 0f });

        var result = zeroA.CosineSimilarity(zeroB);

        Assert.True(float.IsNaN(result), "Both zero vectors should produce NaN");
    }

    [Fact]
    public void CosineSimilarity_WithVeryLargeValues_ReturnsValidSimilarity()
    {
        var a = new ReadOnlyMemory<float>(new float[] { 1e6f, 0f });
        var b = new ReadOnlyMemory<float>(new float[] { 1e6f, 0f });

        var result = a.CosineSimilarity(b);

        Assert.Equal(1.0f, result, precision: 4);
    }

    [Fact]
    public void CosineSimilarity_WithVerySmallValues_ReturnsValidSimilarity()
    {
        var a = new ReadOnlyMemory<float>(new float[] { 1e-6f, 0f });
        var b = new ReadOnlyMemory<float>(new float[] { 1e-6f, 0f });

        var result = a.CosineSimilarity(b);

        Assert.Equal(1.0f, result, precision: 4);
    }

    [Fact]
    public void CosineSimilarity_WithMixedScales_ReturnsValidSimilarity()
    {
        var a = new ReadOnlyMemory<float>(new float[] { 1e6f, 1e-6f });
        var b = new ReadOnlyMemory<float>(new float[] { 1e6f, 1e-6f });

        var result = a.CosineSimilarity(b);

        Assert.Equal(1.0f, result, precision: 3);
    }

    // =========================================================================
    // Batch correctness: Verify SIMD vs manual implementation
    // =========================================================================

    [Theory]
    [InlineData(2)]
    [InlineData(8)]
    [InlineData(384)]
    [InlineData(768)]
    [InlineData(1536)]
    public void CosineSimilarity_ComparesWithManualImplementation(int dimensions)
    {
        var random = new Random(42);
        var a = GenerateRandomVector(dimensions, random);
        var b = GenerateRandomVector(dimensions, random);

        var simdResult = a.CosineSimilarity(b);
        var manualResult = ManualCosineSimilarity(a.Span, b.Span);

        Assert.Equal(manualResult, simdResult, precision: 5);
    }

    [Fact]
    public void CosineSimilarity_WithLargeVectors_MaintainsAccuracy()
    {
        var random = new Random(42);
        var largeVector1 = GenerateRandomVector(10000, random);
        var largeVector2 = GenerateRandomVector(10000, random);

        var result = largeVector1.CosineSimilarity(largeVector2);

        Assert.NotEqual(0f, result);
        Assert.InRange(result, -1f, 1f);
    }

    // =========================================================================
    // Embedding-level CosineSimilarity (wrapper around float[])
    // =========================================================================

    [Fact]
    public void CosineSimilarity_WithEmbeddings_ReturnsCorrectValue()
    {
        var embA = new Embedding<float>(new float[] { 1f, 0f, 0f });
        var embB = new Embedding<float>(new float[] { 0f, 1f, 0f });

        var result = embA.CosineSimilarity(embB);

        Assert.Equal(0f, result, precision: 4);
    }

    [Fact]
    public void CosineSimilarity_WithIdenticalEmbeddings_ReturnsOne()
    {
        var vector = new float[] { 0.6f, 0.8f };
        var embA = new Embedding<float>(vector);
        var embB = new Embedding<float>(vector);

        var result = embA.CosineSimilarity(embB);

        Assert.Equal(1.0f, result, precision: 4);
    }

    // =========================================================================
    // Symmetry and commutativity
    // =========================================================================

    [Fact]
    public void CosineSimilarity_IsCommutative()
    {
        var a = new ReadOnlyMemory<float>(new float[] { 1f, 2f, 3f });
        var b = new ReadOnlyMemory<float>(new float[] { 4f, 5f, 6f });

        var result1 = a.CosineSimilarity(b);
        var result2 = b.CosineSimilarity(a);

        Assert.Equal(result1, result2, precision: 5);
    }

    [Fact]
    public void CosineSimilarity_WithScaledVectors_IsScale_Invariant()
    {
        var a = new ReadOnlyMemory<float>(new float[] { 1f, 2f, 3f });
        var b = new ReadOnlyMemory<float>(new float[] { 4f, 5f, 6f });

        var result1 = a.CosineSimilarity(b);
        var result2 = a.CosineSimilarity(new ReadOnlyMemory<float>(
            new float[] { 8f, 10f, 12f })); // b scaled by 2

        Assert.Equal(result1, result2, precision: 5);
    }

    // =========================================================================
    // Error cases
    // =========================================================================

    [Fact]
    public void CosineSimilarity_WithMismatchedDimensions_ThrowsArgumentException()
    {
        var a = new ReadOnlyMemory<float>(new float[] { 1f, 2f });
        var b = new ReadOnlyMemory<float>(new float[] { 1f, 2f, 3f });

        Assert.Throws<ArgumentException>(() => a.CosineSimilarity(b));
    }

    [Fact]
    public void CosineSimilarity_WithSingleDimension_ReturnsCorrectValue()
    {
        var a = new ReadOnlyMemory<float>(new float[] { 5f });
        var b = new ReadOnlyMemory<float>(new float[] { 5f });

        var result = a.CosineSimilarity(b);

        Assert.Equal(1.0f, result, precision: 4);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static ReadOnlyMemory<float> GenerateRandomVector(int dimensions, Random random)
    {
        var vector = new float[dimensions];
        for (int i = 0; i < dimensions; i++)
        {
            vector[i] = (float)random.NextDouble();
        }
        return new ReadOnlyMemory<float>(vector);
    }

    private static float ManualCosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Vectors must have the same length");

        float dotProduct = 0f;
        float magnitudeA = 0f;
        float magnitudeB = 0f;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        magnitudeA = MathF.Sqrt(magnitudeA);
        magnitudeB = MathF.Sqrt(magnitudeB);

        if (magnitudeA == 0f || magnitudeB == 0f)
            return float.NaN;

        return dotProduct / (magnitudeA * magnitudeB);
    }
}
