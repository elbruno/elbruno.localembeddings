using ElBruno.LocalEmbeddings.Npu.Options;

namespace ElBruno.LocalEmbeddings.Npu.Tests;

public class NpuEmbeddingGeneratorTests
{
    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new NpuEmbeddingGenerator(null!));
    }

    [Fact]
    public void Constructor_InvalidModelPath_ThrowsException()
    {
        var options = new NpuEmbeddingsOptions
        {
            ModelPath = @"C:\nonexistent\path",
            EnsureModelDownloaded = false
        };

        // Should throw because model files don't exist at the path
        Assert.ThrowsAny<Exception>(() => new NpuEmbeddingGenerator(options));
    }

    [Fact]
    public void Constructor_NoModelPathOrDownload_ThrowsInvalidOperationException()
    {
        var options = new NpuEmbeddingsOptions
        {
            EnsureModelDownloaded = false,
            ModelPath = null
        };

        Assert.Throws<InvalidOperationException>(() => new NpuEmbeddingGenerator(options));
    }

    [Fact]
    public async Task CreateAsync_NullOptions_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => NpuEmbeddingGenerator.CreateAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_NoModelPathOrDownload_ThrowsInvalidOperationException()
    {
        var options = new NpuEmbeddingsOptions
        {
            EnsureModelDownloaded = false,
            ModelPath = null
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => NpuEmbeddingGenerator.CreateAsync(options, CancellationToken.None));
    }
}
