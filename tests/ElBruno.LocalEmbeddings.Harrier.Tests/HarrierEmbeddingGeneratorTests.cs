using ElBruno.LocalEmbeddings.Harrier.Options;

namespace ElBruno.LocalEmbeddings.Harrier.Tests;

public class HarrierEmbeddingGeneratorTests
{
    [Fact]
    public async Task CreateAsync_ThrowsOnNullOptions()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => HarrierEmbeddingGenerator.CreateAsync(null!));
    }

    [Fact]
    public async Task CreateAsync_ThrowsWhenNoModelPathAndDownloadDisabled()
    {
        var options = new HarrierEmbeddingsOptions
        {
            EnsureModelDownloaded = false,
            ModelPath = null
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => HarrierEmbeddingGenerator.CreateAsync(options));
    }

    [Fact]
    public async Task CreateAsync_ThrowsWhenModelPathDoesNotExist()
    {
        var options = new HarrierEmbeddingsOptions
        {
            EnsureModelDownloaded = false,
            ModelPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
        };

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => HarrierEmbeddingGenerator.CreateAsync(options));
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task Dispose_IsIdempotent()
    {
        var modelDir = FindHarrierModelDirectory();
        Skip.If(modelDir is null, "Harrier model not available locally — skipping.");

        var generator = await HarrierEmbeddingGenerator.CreateAsync(new HarrierEmbeddingsOptions
        {
            ModelPath = modelDir,
            EnsureModelDownloaded = false
        });

        // Calling Dispose multiple times should not throw
        generator.Dispose();
        generator.Dispose();
        generator.Dispose();
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task DisposeAsync_IsIdempotent()
    {
        var modelDir = FindHarrierModelDirectory();
        Skip.If(modelDir is null, "Harrier model not available locally — skipping.");

        var generator = await HarrierEmbeddingGenerator.CreateAsync(new HarrierEmbeddingsOptions
        {
            ModelPath = modelDir,
            EnsureModelDownloaded = false
        });

        // Calling DisposeAsync multiple times should not throw
        await generator.DisposeAsync();
        await generator.DisposeAsync();
        await generator.DisposeAsync();
    }

    private static string? FindHarrierModelDirectory()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var paths = new[]
        {
            Path.Combine(appData, "ElBruno", "LocalEmbeddings", "models", "onnx-community_harrier-oss-v1-270m-ONNX"),
            Path.Combine(appData, "LocalEmbeddings", "models", "onnx-community_harrier-oss-v1-270m-ONNX"),
        };

        return paths.FirstOrDefault(p =>
            Directory.Exists(p) &&
            File.Exists(Path.Combine(p, "tokenizer.json")) &&
            (File.Exists(Path.Combine(p, "model.onnx")) ||
             File.Exists(Path.Combine(p, "model_quantized.onnx")) ||
             File.Exists(Path.Combine(p, "model_q4.onnx")) ||
             File.Exists(Path.Combine(p, "model_fp16.onnx"))));
    }
}
