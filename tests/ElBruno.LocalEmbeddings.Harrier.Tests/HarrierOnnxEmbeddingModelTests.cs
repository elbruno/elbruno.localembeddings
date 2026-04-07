using Microsoft.ML.OnnxRuntime.Tensors;

namespace ElBruno.LocalEmbeddings.Harrier.Tests;

public class HarrierOnnxEmbeddingModelTests
{
    [Fact]
    public void ExtractEmbeddings_ReturnsCorrectVectors()
    {
        // Simulate a 2D output tensor [batch=2, dim=3]
        var data = new float[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f };
        var tensor = new DenseTensor<float>(data, [2, 3]);

        var result = HarrierOnnxEmbeddingModel.ExtractEmbeddings(tensor, 2);

        Assert.Equal(2, result.Length);
        Assert.Equal([1.0f, 2.0f, 3.0f], result[0]);
        Assert.Equal([4.0f, 5.0f, 6.0f], result[1]);
    }

    [Fact]
    public void ExtractEmbeddings_SingleBatch()
    {
        var data = new float[] { 0.5f, -0.5f, 1.0f, 0.0f };
        var tensor = new DenseTensor<float>(data, [1, 4]);

        var result = HarrierOnnxEmbeddingModel.ExtractEmbeddings(tensor, 1);

        Assert.Single(result);
        Assert.Equal([0.5f, -0.5f, 1.0f, 0.0f], result[0]);
    }

    [Fact]
    public void Load_ThrowsOnNullPath()
    {
        using var model = new HarrierOnnxEmbeddingModel();
        Assert.Throws<ArgumentException>(() => model.Load(""));
    }

    [Fact]
    public void Load_ThrowsOnMissingFile()
    {
        using var model = new HarrierOnnxEmbeddingModel();
        Assert.Throws<FileNotFoundException>(() => model.Load("/nonexistent/model.onnx"));
    }

    [Fact]
    public void Load_ThrowsOnInvalidThreadCount()
    {
        using var model = new HarrierOnnxEmbeddingModel();
        var tempFile = Path.GetTempFileName();
        try
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => model.Load(tempFile, interOpNumThreads: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => model.Load(tempFile, intraOpNumThreads: -1));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void GenerateEmbedding_ThrowsWhenNotLoaded()
    {
        using var model = new HarrierOnnxEmbeddingModel();
        Assert.Throws<InvalidOperationException>(
            () => model.GenerateEmbedding(new long[] { 1, 2, 3 }, new long[] { 1, 1, 1 }));
    }

    [Fact]
    public void GenerateEmbedding_ThrowsOnMismatchedLengths()
    {
        using var model = new HarrierOnnxEmbeddingModel();
        // Model not loaded but length check happens after null checks
        Assert.Throws<ArgumentException>(
            () => model.GenerateEmbedding(new long[] { 1, 2, 3 }, new long[] { 1, 1 }));
    }

    [Fact]
    public void IsLoaded_FalseByDefault()
    {
        using var model = new HarrierOnnxEmbeddingModel();
        Assert.False(model.IsLoaded);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var model = new HarrierOnnxEmbeddingModel();
        model.Dispose();
        model.Dispose(); // Should not throw
    }

    [Fact]
    public void GenerateEmbeddings_ThrowsAfterDispose()
    {
        var model = new HarrierOnnxEmbeddingModel();
        model.Dispose();

        Assert.Throws<ObjectDisposedException>(
            () => model.GenerateEmbeddings(
                [new long[] { 1 }],
                [new long[] { 1 }]));
    }
}
