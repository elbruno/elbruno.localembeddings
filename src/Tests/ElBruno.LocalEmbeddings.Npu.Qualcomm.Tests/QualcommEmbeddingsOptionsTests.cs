using ElBruno.LocalEmbeddings.Npu.Qualcomm.Options;

namespace ElBruno.LocalEmbeddings.Npu.Qualcomm.Tests;

public class QualcommEmbeddingsOptionsTests
{
    [Fact]
    public void DefaultOptions_HasExpectedDefaults()
    {
        var options = new QualcommEmbeddingsOptions();

        Assert.Equal("sentence-transformers/all-MiniLM-L6-v2", options.ModelName);
        Assert.Null(options.ModelPath);
        Assert.Null(options.CacheDirectory);
        Assert.Equal(512, options.MaxSequenceLength);
        Assert.True(options.EnsureModelDownloaded);
        Assert.False(options.NormalizeEmbeddings);
        Assert.True(options.PreferQuantized);
        Assert.Equal("QnnHtp.dll", options.QnnBackendPath);
        Assert.True(options.FallbackToCpu);
        Assert.Null(options.ExpectedHash);
    }

    [Fact]
    public void PreferQuantized_DefaultsToTrue_ForQualcommNpu()
    {
        var options = new QualcommEmbeddingsOptions();
        Assert.True(options.PreferQuantized);
    }

    [Fact]
    public void FallbackToCpu_DefaultsToTrue()
    {
        var options = new QualcommEmbeddingsOptions();
        Assert.True(options.FallbackToCpu);
    }

    [Fact]
    public void Options_CanBeCustomized()
    {
        var options = new QualcommEmbeddingsOptions
        {
            ModelName = "custom-model",
            ModelPath = @"C:\models\custom",
            CacheDirectory = @"C:\cache",
            MaxSequenceLength = 256,
            EnsureModelDownloaded = false,
            NormalizeEmbeddings = true,
            PreferQuantized = false,
            QnnBackendPath = "QnnCpu.dll",
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
        Assert.Equal("QnnCpu.dll", options.QnnBackendPath);
        Assert.False(options.FallbackToCpu);
        Assert.Equal("abc123", options.ExpectedHash);
    }
}
