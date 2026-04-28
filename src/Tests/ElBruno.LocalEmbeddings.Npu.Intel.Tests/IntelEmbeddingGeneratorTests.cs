using ElBruno.LocalEmbeddings.Npu.Intel.Options;

namespace ElBruno.LocalEmbeddings.Npu.Intel.Tests;

public class IntelEmbeddingGeneratorTests
{
    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new IntelEmbeddingGenerator(null!));
    }

    [Fact]
    public void Constructor_InvalidModelPath_ThrowsException()
    {
        var options = new IntelEmbeddingsOptions
        {
            ModelPath = @"C:\nonexistent\path",
            EnsureModelDownloaded = false
        };

        Assert.ThrowsAny<Exception>(() => new IntelEmbeddingGenerator(options));
    }

    [Fact]
    public void Constructor_NoModelPathOrDownload_ThrowsInvalidOperationException()
    {
        var options = new IntelEmbeddingsOptions
        {
            EnsureModelDownloaded = false,
            ModelPath = null
        };

        Assert.Throws<InvalidOperationException>(() => new IntelEmbeddingGenerator(options));
    }

    [Fact]
    public async Task CreateAsync_NullOptions_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => IntelEmbeddingGenerator.CreateAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_NoModelPathOrDownload_ThrowsInvalidOperationException()
    {
        var options = new IntelEmbeddingsOptions
        {
            EnsureModelDownloaded = false,
            ModelPath = null
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => IntelEmbeddingGenerator.CreateAsync(options, CancellationToken.None));
    }
}
