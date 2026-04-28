using ElBruno.LocalEmbeddings.Options;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalEmbeddings.Tests;

public class ConcurrencyTests
{
    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task GenerateAsync_ConcurrentCalls_AllReturnValidResults()
    {
        var modelPath = GetModelPath();
        Skip.If(modelPath is null, "Model not available for testing");

        var options = new LocalEmbeddingsOptions
        {
            ModelPath = modelPath,
            EnsureModelDownloaded = false
        };

        using var generator = new LocalEmbeddingGenerator(options);

        var tasks = Enumerable.Range(0, 10)
            .Select(i => generator.GenerateAsync([$"Concurrent call number {i}"]))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(10, results.Length);
        foreach (var result in results)
        {
            Assert.Single(result);
            Assert.Equal(384, result[0].Vector.Length);
            Assert.True(result[0].Vector.ToArray().Any(v => v != 0f), "Embedding should have non-zero values");
        }
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
