namespace ElBruno.LocalEmbeddings.Npu.Tests;

public class NpuOnnxEmbeddingModelTests
{
    [Fact]
    public void Load_NullPath_ThrowsArgumentException()
    {
        using var model = new NpuOnnxEmbeddingModel();
        Assert.Throws<ArgumentException>(() => model.Load(null!));
    }

    [Fact]
    public void Load_EmptyPath_ThrowsArgumentException()
    {
        using var model = new NpuOnnxEmbeddingModel();
        Assert.Throws<ArgumentException>(() => model.Load(string.Empty));
    }

    [Fact]
    public void Load_NonExistentFile_ThrowsFileNotFoundException()
    {
        using var model = new NpuOnnxEmbeddingModel();
        Assert.Throws<FileNotFoundException>(() => model.Load(@"C:\nonexistent\model.onnx"));
    }

    [Fact]
    public void Load_NegativeDeviceId_ThrowsArgumentOutOfRangeException()
    {
        // Create a temp file so the FileNotFoundException check passes first
        var tempFile = Path.GetTempFileName();
        try
        {
            using var model = new NpuOnnxEmbeddingModel();
            Assert.Throws<ArgumentOutOfRangeException>(() => model.Load(tempFile, deviceId: -1));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void IsLoaded_BeforeLoad_ReturnsFalse()
    {
        using var model = new NpuOnnxEmbeddingModel();
        Assert.False(model.IsLoaded);
    }

    [Fact]
    public void GenerateEmbedding_BeforeLoad_ThrowsInvalidOperationException()
    {
        using var model = new NpuOnnxEmbeddingModel();
        Assert.Throws<InvalidOperationException>(
            () => model.GenerateEmbedding([1, 2, 3], [1, 1, 1]));
    }

    [Fact]
    public void GenerateEmbedding_NullInputIds_ThrowsArgumentNullException()
    {
        using var model = new NpuOnnxEmbeddingModel();
        Assert.Throws<ArgumentNullException>(
            () => model.GenerateEmbedding(null!, [1, 2, 3]));
    }

    [Fact]
    public void GenerateEmbeddings_MismatchedLengths_ThrowsArgumentException()
    {
        using var model = new NpuOnnxEmbeddingModel();
        Assert.Throws<ArgumentException>(
            () => model.GenerateEmbeddings([[1, 2]], [[1, 2], [3, 4]]));
    }

    [Fact]
    public void GenerateEmbeddings_EmptyInput_ReturnsEmptyWithoutLoadedModel()
    {
        using var model = new NpuOnnxEmbeddingModel();
        // Empty input returns empty array even without a loaded model
        var result = model.GenerateEmbeddings([], []);
        Assert.Empty(result);
    }

    [Fact]
    public void Dispose_MultipleTimes_DoesNotThrow()
    {
        var model = new NpuOnnxEmbeddingModel();
        model.Dispose();
        model.Dispose(); // Should not throw
    }

    [Fact]
    public void AfterDispose_Load_ThrowsObjectDisposedException()
    {
        var model = new NpuOnnxEmbeddingModel();
        model.Dispose();
        Assert.Throws<ObjectDisposedException>(() => model.Load("test.onnx"));
    }

    [Theory]
    [MemberData(nameof(MeanPoolingTestData))]
    public void ApplyMeanPooling_ProducesCorrectResults(
        float[] tensorData, int[] dims, long[][] masks,
        int batchSize, int seqLen, float[][] expected)
    {
        var tensor = new Microsoft.ML.OnnxRuntime.Tensors.DenseTensor<float>(
            tensorData, dims.Select(d => d).ToArray());

        var result = NpuOnnxEmbeddingModel.ApplyMeanPooling(tensor, masks, batchSize, seqLen);

        Assert.Equal(expected.Length, result.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].Length, result[i].Length);
            for (int j = 0; j < expected[i].Length; j++)
            {
                Assert.Equal(expected[i][j], result[i][j], precision: 5);
            }
        }
    }

    public static TheoryData<float[], int[], long[][], int, int, float[][]> MeanPoolingTestData =>
        new()
        {
            {
                // Single batch, 2 tokens, 3 hidden dims, all attended
                new float[] { 1, 2, 3, 4, 5, 6 },
                new[] { 1, 2, 3 },
                new[] { new long[] { 1, 1 } },
                1, 2,
                new[] { new[] { 2.5f, 3.5f, 4.5f } }
            },
            {
                // Single batch, 2 tokens, 3 hidden dims, one masked
                new float[] { 1, 2, 3, 4, 5, 6 },
                new[] { 1, 2, 3 },
                new[] { new long[] { 1, 0 } },
                1, 2,
                new[] { new[] { 1f, 2f, 3f } }
            }
        };
}
