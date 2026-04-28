using ElBruno.LocalEmbeddings.Npu.Intel.Options;

namespace ElBruno.LocalEmbeddings.Npu.Intel.Tests;

public class IntelEmbeddingsOptionsTests
{
    [Fact]
    public void DefaultOptions_HasExpectedDefaults()
    {
        var options = new IntelEmbeddingsOptions();

        Assert.Equal("sentence-transformers/all-MiniLM-L6-v2", options.ModelName);
        Assert.Null(options.ModelPath);
        Assert.Null(options.CacheDirectory);
        Assert.Equal(512, options.MaxSequenceLength);
        Assert.True(options.EnsureModelDownloaded);
        Assert.False(options.NormalizeEmbeddings);
        Assert.True(options.PreferQuantized);
        Assert.Equal("NPU", options.DeviceType);
        Assert.True(options.FallbackToCpu);
        Assert.Null(options.ExpectedHash);
    }

    [Fact]
    public void PreferQuantized_DefaultsToTrue_ForIntelNpu()
    {
        var options = new IntelEmbeddingsOptions();
        Assert.True(options.PreferQuantized);
    }

    [Fact]
    public void DeviceType_DefaultsToNPU()
    {
        var options = new IntelEmbeddingsOptions();
        Assert.Equal("NPU", options.DeviceType);
    }

    [Fact]
    public void FallbackToCpu_DefaultsToTrue()
    {
        var options = new IntelEmbeddingsOptions();
        Assert.True(options.FallbackToCpu);
    }

    [Fact]
    public void Options_CanBeCustomized()
    {
        var options = new IntelEmbeddingsOptions
        {
            ModelName = "custom-model",
            ModelPath = @"C:\models\custom",
            CacheDirectory = @"C:\cache",
            MaxSequenceLength = 256,
            EnsureModelDownloaded = false,
            NormalizeEmbeddings = true,
            PreferQuantized = false,
            DeviceType = "CPU",
            FallbackToCpu = false,
            ExpectedHash = "abc123"
        };

        Assert.Equal("custom-model", options.ModelName);
        Assert.Equal(@"C:\models\custom", options.ModelPath);
        Assert.Equal(@"C:\cache", options.CacheDirectory);
        Assert.Equal(256, options.MaxSequenceLength);
        Assert.False(options.EnsureModelDownloaded);
        Assert.True(options.NormalizeEmbeddings);
        Assert.False(options.PreferQuantized);
        Assert.Equal("CPU", options.DeviceType);
        Assert.False(options.FallbackToCpu);
        Assert.Equal("abc123", options.ExpectedHash);
    }
}
