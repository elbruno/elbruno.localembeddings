using ElBruno.LocalEmbeddings.Npu.Options;

namespace ElBruno.LocalEmbeddings.Npu.Tests;

public class NpuEmbeddingsOptionsTests
{
    [Fact]
    public void DefaultOptions_HasExpectedDefaults()
    {
        var options = new NpuEmbeddingsOptions();

        Assert.Equal("sentence-transformers/all-MiniLM-L6-v2", options.ModelName);
        Assert.Null(options.ModelPath);
        Assert.Null(options.CacheDirectory);
        Assert.Equal(512, options.MaxSequenceLength);
        Assert.True(options.EnsureModelDownloaded);
        Assert.False(options.NormalizeEmbeddings);
        Assert.True(options.PreferQuantized);
        Assert.Equal(0, options.DeviceId);
        Assert.Null(options.ExpectedHash);
    }

    [Fact]
    public void PreferQuantized_DefaultsToTrue_ForNpu()
    {
        var options = new NpuEmbeddingsOptions();

        // NPU should prefer quantized models by default for optimal performance
        Assert.True(options.PreferQuantized);
    }

    [Fact]
    public void Options_CanBeCustomized()
    {
        var options = new NpuEmbeddingsOptions
        {
            ModelName = "custom-model",
            ModelPath = @"C:\models\custom",
            CacheDirectory = @"C:\cache",
            MaxSequenceLength = 256,
            EnsureModelDownloaded = false,
            NormalizeEmbeddings = true,
            PreferQuantized = false,
            DeviceId = 1,
            ExpectedHash = "abc123"
        };

        Assert.Equal("custom-model", options.ModelName);
        Assert.Equal(@"C:\models\custom", options.ModelPath);
        Assert.Equal(@"C:\cache", options.CacheDirectory);
        Assert.Equal(256, options.MaxSequenceLength);
        Assert.False(options.EnsureModelDownloaded);
        Assert.True(options.NormalizeEmbeddings);
        Assert.False(options.PreferQuantized);
        Assert.Equal(1, options.DeviceId);
        Assert.Equal("abc123", options.ExpectedHash);
    }
}
