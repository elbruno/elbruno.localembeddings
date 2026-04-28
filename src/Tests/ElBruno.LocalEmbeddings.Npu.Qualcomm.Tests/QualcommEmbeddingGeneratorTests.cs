using ElBruno.LocalEmbeddings.Npu.Qualcomm.Options;

namespace ElBruno.LocalEmbeddings.Npu.Qualcomm.Tests;

public class QualcommEmbeddingGeneratorTests
{
    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new QualcommEmbeddingGenerator(null!));
    }

    [Fact]
    public void Constructor_InvalidModelPath_ThrowsException()
    {
        var options = new QualcommEmbeddingsOptions
        {
            ModelPath = @"C:\nonexistent\path",
            EnsureModelDownloaded = false
        };

        Assert.ThrowsAny<Exception>(() => new QualcommEmbeddingGenerator(options));
    }

    [Fact]
    public void Constructor_NoModelPathOrDownload_ThrowsInvalidOperationException()
    {
        var options = new QualcommEmbeddingsOptions
        {
            EnsureModelDownloaded = false,
            ModelPath = null
        };

        Assert.Throws<InvalidOperationException>(() => new QualcommEmbeddingGenerator(options));
    }

    [Fact]
    public async Task CreateAsync_NullOptions_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => QualcommEmbeddingGenerator.CreateAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_NoModelPathOrDownload_ThrowsInvalidOperationException()
    {
        var options = new QualcommEmbeddingsOptions
        {
            EnsureModelDownloaded = false,
            ModelPath = null
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => QualcommEmbeddingGenerator.CreateAsync(options, CancellationToken.None));
    }
}
