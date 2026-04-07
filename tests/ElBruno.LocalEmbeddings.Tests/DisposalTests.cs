using ElBruno.LocalEmbeddings.Options;

namespace ElBruno.LocalEmbeddings.Tests;

public class DisposalTests
{
    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task DisposeAsync_ReleasesResources()
    {
        var modelPath = GetModelPath();
        Skip.If(modelPath is null, "Model not available for testing");

        var options = new LocalEmbeddingsOptions
        {
            ModelPath = modelPath,
            EnsureModelDownloaded = false
        };

        var generator = new LocalEmbeddingGenerator(options);

        // Verify it works before disposal
        var result = await generator.GenerateAsync(["test"]);
        Assert.Single(result);

        // Dispose asynchronously
        await generator.DisposeAsync();

        // Verify it's actually disposed — should throw
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => generator.GenerateAsync(["should fail"]));
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task GenerateAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var modelPath = GetModelPath();
        Skip.If(modelPath is null, "Model not available for testing");

        var options = new LocalEmbeddingsOptions
        {
            ModelPath = modelPath,
            EnsureModelDownloaded = false
        };

        var generator = new LocalEmbeddingGenerator(options);
        generator.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => generator.GenerateAsync(["test after dispose"]));
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
