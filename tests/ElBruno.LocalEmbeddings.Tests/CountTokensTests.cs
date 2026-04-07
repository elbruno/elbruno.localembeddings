using ElBruno.LocalEmbeddings.Options;

namespace ElBruno.LocalEmbeddings.Tests;

public class CountTokensTests
{
    [SkippableFact]
    [Trait("Category", "Integration")]
    public void CountTokens_ReturnsPositiveCount()
    {
        var modelPath = GetModelPath();
        Skip.If(modelPath is null, "Model not available for testing");

        var options = new LocalEmbeddingsOptions
        {
            ModelPath = modelPath,
            EnsureModelDownloaded = false
        };

        using var generator = new LocalEmbeddingGenerator(options);

        int count = generator.CountTokens("Hello world, this is a test sentence.");

        Assert.True(count > 0, $"Expected positive token count, got {count}");
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public void CountTokens_EmptyString_ReturnsCount()
    {
        var modelPath = GetModelPath();
        Skip.If(modelPath is null, "Model not available for testing");

        var options = new LocalEmbeddingsOptions
        {
            ModelPath = modelPath,
            EnsureModelDownloaded = false
        };

        using var generator = new LocalEmbeddingGenerator(options);

        // Empty string should still produce a count (at least special tokens)
        int count = generator.CountTokens("");

        Assert.True(count >= 0, $"Expected non-negative token count for empty string, got {count}");
    }

    private static string? GetModelPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var paths = new[]
        {
            Path.Combine(appData, "ElBruno", "LocalEmbeddings", "models", "sentence-transformers_all-MiniLM-L6-v2"),
            Path.Combine(appData, "LocalEmbeddings", "models", "sentence-transformers_all-MiniLM-L6-v2"),
        };

        return paths.FirstOrDefault(p =>
            Directory.Exists(p) && File.Exists(Path.Combine(p, "model.onnx")));
    }
}
