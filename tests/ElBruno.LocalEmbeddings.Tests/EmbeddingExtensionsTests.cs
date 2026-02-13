using ElBruno.LocalEmbeddings.Extensions;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalEmbeddings.Tests;

public class EmbeddingExtensionsTests
{
    [Fact]
    public void Similarity_WithSingleCollection_ReturnsSquareMatrix()
    {
        var embeddings = new[]
        {
            CreateEmbedding([1f, 0f]),
            CreateEmbedding([0f, 1f]),
            CreateEmbedding([1f, 1f])
        };

        var matrix = embeddings.Similarity();

        Assert.Equal(3, matrix.GetLength(0));
        Assert.Equal(3, matrix.GetLength(1));

        Assert.Equal(1f, matrix[0, 0], 4);
        Assert.Equal(1f, matrix[1, 1], 4);
        Assert.Equal(1f, matrix[2, 2], 4);

        Assert.Equal(0f, matrix[0, 1], 4);
        Assert.Equal(0f, matrix[1, 0], 4);
    }

    [Fact]
    public void Similarity_WithTwoCollections_ReturnsAllPairsMatrix()
    {
        var left = new[]
        {
            CreateEmbedding([1f, 0f]),
            CreateEmbedding([0f, 1f])
        };

        var right = new[]
        {
            CreateEmbedding([1f, 0f]),
            CreateEmbedding([1f, 1f])
        };

        var matrix = left.Similarity(right);

        Assert.Equal(2, matrix.GetLength(0));
        Assert.Equal(2, matrix.GetLength(1));

        var sqrt2Over2 = MathF.Sqrt(2f) / 2f;

        Assert.Equal(1f, matrix[0, 0], 4);
        Assert.Equal(sqrt2Over2, matrix[0, 1], 4);
        Assert.Equal(0f, matrix[1, 0], 4);
        Assert.Equal(sqrt2Over2, matrix[1, 1], 4);
    }

    [Fact]
    public void Similarity_WithNullFirstCollection_ThrowsArgumentNullException()
    {
        IEnumerable<Embedding<float>> left = null!;
        var right = new[] { CreateEmbedding([1f, 0f]) };

        Assert.Throws<ArgumentNullException>(() => left.Similarity(right));
    }

    [Fact]
    public void Similarity_WithNullSecondCollection_ThrowsArgumentNullException()
    {
        var left = new[] { CreateEmbedding([1f, 0f]) };
        IEnumerable<Embedding<float>> right = null!;

        Assert.Throws<ArgumentNullException>(() => left.Similarity(right));
    }

    [Fact]
    public void Similarity_WithMismatchedDimensions_ThrowsArgumentException()
    {
        var left = new[] { CreateEmbedding([1f, 0f]) };
        var right = new[] { CreateEmbedding([1f, 0f, 0f]) };

        Assert.Throws<ArgumentException>(() => left.Similarity(right));
    }

    [Fact]
    public void Similarity_WithEmptyCollections_ReturnsEmptyMatrix()
    {
        var matrix = Array.Empty<Embedding<float>>().Similarity(Array.Empty<Embedding<float>>());

        Assert.Equal(0, matrix.GetLength(0));
        Assert.Equal(0, matrix.GetLength(1));
    }

    [Fact]
    public void CosineSimilarity_WithOrthogonalVectors_ReturnsZero()
    {
        var a = CreateEmbedding([1f, 0f]);
        var b = CreateEmbedding([0f, 1f]);

        var similarity = a.CosineSimilarity(b);

        Assert.Equal(0f, similarity, 4);
    }

    private static Embedding<float> CreateEmbedding(float[] vector) => new(vector);
}
