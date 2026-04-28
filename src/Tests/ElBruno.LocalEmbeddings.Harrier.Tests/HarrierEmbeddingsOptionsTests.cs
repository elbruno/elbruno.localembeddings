using ElBruno.LocalEmbeddings.Harrier.Options;

namespace ElBruno.LocalEmbeddings.Harrier.Tests;

public class HarrierEmbeddingsOptionsTests
{
    [Fact]
    public void DefaultOptions_HasExpectedDefaults()
    {
        var options = new HarrierEmbeddingsOptions();

        Assert.Equal(HarrierEmbeddingsOptions.DefaultModelName, options.ModelName);
        Assert.Null(options.ModelPath);
        Assert.Null(options.CacheDirectory);
        Assert.Equal(8192, options.MaxSequenceLength);
        Assert.True(options.EnsureModelDownloaded);
        Assert.True(options.UseParallelExecution);
        Assert.Equal(HarrierModelVariant.Quantized, options.ModelVariant);
        Assert.Equal(HarrierEmbeddingsOptions.DefaultInstructionPrefix, options.InstructionPrefix);
        Assert.Null(options.InterOpNumThreads);
        Assert.Null(options.IntraOpNumThreads);
        Assert.Null(options.ExpectedHash);
    }

    [Fact]
    public void DefaultModelName_IsCorrectRepo()
    {
        Assert.Equal("onnx-community/harrier-oss-v1-270m-ONNX", HarrierEmbeddingsOptions.DefaultModelName);
    }

    [Fact]
    public void DefaultInstructionPrefix_IsRetrievalInstruction()
    {
        Assert.StartsWith("Instruct:", HarrierEmbeddingsOptions.DefaultInstructionPrefix);
        Assert.Contains("Query:", HarrierEmbeddingsOptions.DefaultInstructionPrefix);
    }

    [Fact]
    public void Options_CanBeCustomized()
    {
        var options = new HarrierEmbeddingsOptions
        {
            ModelName = "custom/model",
            ModelPath = "/path/to/model",
            CacheDirectory = "/cache",
            MaxSequenceLength = 4096,
            EnsureModelDownloaded = false,
            UseParallelExecution = false,
            ModelVariant = HarrierModelVariant.Q4,
            InstructionPrefix = "Custom: ",
            InterOpNumThreads = 4,
            IntraOpNumThreads = 2,
            ExpectedHash = "abc123"
        };

        Assert.Equal("custom/model", options.ModelName);
        Assert.Equal("/path/to/model", options.ModelPath);
        Assert.Equal("/cache", options.CacheDirectory);
        Assert.Equal(4096, options.MaxSequenceLength);
        Assert.False(options.EnsureModelDownloaded);
        Assert.False(options.UseParallelExecution);
        Assert.Equal(HarrierModelVariant.Q4, options.ModelVariant);
        Assert.Equal("Custom: ", options.InstructionPrefix);
        Assert.Equal(4, options.InterOpNumThreads);
        Assert.Equal(2, options.IntraOpNumThreads);
        Assert.Equal("abc123", options.ExpectedHash);
    }

    [Fact]
    public void InstructionPrefix_CanBeSetToNull()
    {
        var options = new HarrierEmbeddingsOptions { InstructionPrefix = null };
        Assert.Null(options.InstructionPrefix);
    }
}
