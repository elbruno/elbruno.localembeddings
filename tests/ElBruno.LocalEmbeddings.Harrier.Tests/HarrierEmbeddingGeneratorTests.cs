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
}
