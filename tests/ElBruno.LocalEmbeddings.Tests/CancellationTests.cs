using ElBruno.LocalEmbeddings.Options;

namespace ElBruno.LocalEmbeddings.Tests;

public class CancellationTests
{
    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task GenerateAsync_WhenCancelled_ThrowsOperationCancelledException()
    {
        var modelPath = GetModelPath();
        Skip.If(modelPath is null, "Model not available for testing");

        var options = new LocalEmbeddingsOptions
        {
            ModelPath = modelPath,
            EnsureModelDownloaded = false
        };

        using var generator = new LocalEmbeddingGenerator(options);
        using var cts = new CancellationTokenSource();

        // Cancel before calling GenerateAsync
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => generator.GenerateAsync(["This should be cancelled"], cancellationToken: cts.Token));
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
