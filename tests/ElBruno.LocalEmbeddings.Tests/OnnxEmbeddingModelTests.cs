namespace ElBruno.LocalEmbeddings.Tests;

public class OnnxEmbeddingModelTests
{
    [Fact]
    public void Load_WithNullOrWhitespacePath_ThrowsArgumentException()
    {
        using var model = new OnnxEmbeddingModel();

        Assert.Throws<ArgumentException>(() => model.Load(null!));
        Assert.Throws<ArgumentException>(() => model.Load(""));
        Assert.Throws<ArgumentException>(() => model.Load("  "));
    }

    [Fact]
    public void Load_WithMissingFile_ThrowsFileNotFoundException()
    {
        using var model = new OnnxEmbeddingModel();

        Assert.Throws<FileNotFoundException>(() => model.Load("c:/does-not-exist/model.onnx"));
    }

    [Fact]
    public void Load_WithInvalidThreadCounts_ThrowsArgumentOutOfRangeException()
    {
        var tempModelPath = Path.GetTempFileName();
        using var model = new OnnxEmbeddingModel();

        try
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                model.Load(tempModelPath, interOpNumThreads: 0));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                model.Load(tempModelPath, intraOpNumThreads: -1));
        }
        finally
        {
            if (File.Exists(tempModelPath))
            {
                File.Delete(tempModelPath);
            }
        }
    }

    [Fact]
    public void GenerateEmbedding_WithoutLoad_ThrowsInvalidOperationException()
    {
        using var model = new OnnxEmbeddingModel();

        Assert.Throws<InvalidOperationException>(() =>
            model.GenerateEmbedding([1, 2, 3], [1, 1, 1]));
    }

    [Fact]
    public void GenerateEmbeddings_WithoutLoad_ThrowsInvalidOperationException()
    {
        using var model = new OnnxEmbeddingModel();

        Assert.Throws<InvalidOperationException>(() =>
            model.GenerateEmbeddings([[1, 2]], [[1, 1]]));
    }

    [Fact]
    public void GenerateEmbeddings_WithMismatchedBatchLengths_ThrowsArgumentException()
    {
        using var model = new OnnxEmbeddingModel();

        Assert.Throws<ArgumentException>(() =>
            model.GenerateEmbeddings([[1, 2], [3, 4]], [[1, 1]]));
    }

    [Fact]
    public void GenerateEmbeddings_WithEmptyInput_ReturnsEmpty()
    {
        using var model = new OnnxEmbeddingModel();

        var result = model.GenerateEmbeddings([], []);

        Assert.Empty(result);
    }

    [Fact]
    public void Dispose_MultipleTimes_DoesNotThrow()
    {
        var model = new OnnxEmbeddingModel();

        model.Dispose();
        model.Dispose();
        model.Dispose();
    }
}
