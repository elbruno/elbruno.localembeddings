using ElBruno.LocalEmbeddings.ImageEmbeddings.Options;

namespace ElBruno.LocalEmbeddings.ImageEmbeddings.Tests;

public class ImageEmbeddingsOptionsTests
{
    [Fact]
    public void DefaultValues_AreSetCorrectly()
    {
        var options = new ImageEmbeddingsOptions();

        Assert.Equal(string.Empty, options.ModelDirectory);
        Assert.Equal("text_model.onnx", options.TextModelFileName);
        Assert.Equal("vision_model.onnx", options.VisionModelFileName);
        Assert.Equal("vocab.json", options.VocabFileName);
        Assert.Equal("merges.txt", options.MergesFileName);
    }

    [Fact]
    public void ComputedPaths_CombineDirectoryAndFileName()
    {
        var options = new ImageEmbeddingsOptions
        {
            ModelDirectory = "/models/clip"
        };

        Assert.Equal(Path.Combine("/models/clip", "text_model.onnx"), options.TextModelPath);
        Assert.Equal(Path.Combine("/models/clip", "vision_model.onnx"), options.VisionModelPath);
        Assert.Equal(Path.Combine("/models/clip", "vocab.json"), options.VocabPath);
        Assert.Equal(Path.Combine("/models/clip", "merges.txt"), options.MergesPath);
    }

    [Fact]
    public void CustomFileNames_AreUsedInPaths()
    {
        var options = new ImageEmbeddingsOptions
        {
            ModelDirectory = "/my/models",
            TextModelFileName = "custom_text.onnx",
            VisionModelFileName = "custom_vision.onnx",
            VocabFileName = "custom_vocab.json",
            MergesFileName = "custom_merges.txt"
        };

        Assert.Equal(Path.Combine("/my/models", "custom_text.onnx"), options.TextModelPath);
        Assert.Equal(Path.Combine("/my/models", "custom_vision.onnx"), options.VisionModelPath);
        Assert.Equal(Path.Combine("/my/models", "custom_vocab.json"), options.VocabPath);
        Assert.Equal(Path.Combine("/my/models", "custom_merges.txt"), options.MergesPath);
    }
}
