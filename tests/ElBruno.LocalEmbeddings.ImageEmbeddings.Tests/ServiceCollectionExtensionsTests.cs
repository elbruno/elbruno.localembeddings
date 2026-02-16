using ElBruno.LocalEmbeddings.ImageEmbeddings.Extensions;
using ElBruno.LocalEmbeddings.ImageEmbeddings.Options;
using Microsoft.Extensions.DependencyInjection;

namespace ElBruno.LocalEmbeddings.ImageEmbeddings.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddImageEmbeddings_WithNullConfigure_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
            services.AddImageEmbeddings((Action<ImageEmbeddingsOptions>)null!));
    }

    [Fact]
    public void AddImageEmbeddings_WithNullOptions_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
            services.AddImageEmbeddings((ImageEmbeddingsOptions)null!));
    }
}
