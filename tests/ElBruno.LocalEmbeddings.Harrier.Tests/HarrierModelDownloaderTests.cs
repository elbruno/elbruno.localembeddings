using ElBruno.LocalEmbeddings.Harrier.Options;

namespace ElBruno.LocalEmbeddings.Harrier.Tests;

public class HarrierModelDownloaderTests
{
    [Theory]
    [InlineData(HarrierModelVariant.Fp32, "model.onnx")]
    [InlineData(HarrierModelVariant.Fp16, "model_fp16.onnx")]
    [InlineData(HarrierModelVariant.Quantized, "model_quantized.onnx")]
    [InlineData(HarrierModelVariant.Q4, "model_q4.onnx")]
    public void GetOnnxFileName_ReturnsCorrectName(HarrierModelVariant variant, string expectedFileName)
    {
        var fileName = HarrierModelDownloader.GetOnnxFileName(variant);
        Assert.Equal(expectedFileName, fileName);
    }

    [Fact]
    public void ResolveModelPath_ThrowsWhenNoModelFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            Assert.Throws<FileNotFoundException>(
                () => HarrierModelDownloader.ResolveModelPath(tempDir, HarrierModelVariant.Quantized));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ResolveModelPath_FindsVariantFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var expectedPath = Path.Combine(tempDir, "model_quantized.onnx");
            File.WriteAllText(expectedPath, "dummy");

            var result = HarrierModelDownloader.ResolveModelPath(tempDir, HarrierModelVariant.Quantized);
            Assert.Equal(expectedPath, result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ResolveModelPath_FallsBackToDefaultModel()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var defaultPath = Path.Combine(tempDir, "model.onnx");
            File.WriteAllText(defaultPath, "dummy");

            // Request Q4 but only model.onnx exists — should fall back
            var result = HarrierModelDownloader.ResolveModelPath(tempDir, HarrierModelVariant.Q4);
            Assert.Equal(defaultPath, result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Constructor_ThrowsOnNullOptions()
    {
        Assert.Throws<ArgumentNullException>(() => new HarrierModelDownloader(null!));
    }

    [Fact]
    public void Constructor_ThrowsOnNullHttpClient()
    {
        var options = new HarrierEmbeddingsOptions();
        Assert.Throws<ArgumentNullException>(() => new HarrierModelDownloader(null!, options));
    }
}
