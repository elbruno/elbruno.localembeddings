using ElBruno.LocalEmbeddings.Npu.Extensions;
using ElBruno.LocalEmbeddings.Npu.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace ElBruno.LocalEmbeddings.Npu.Tests;

public class NpuServiceCollectionExtensionsTests
{
    [Fact]
    public void AddLocalEmbeddingsNpu_RegistersOptions()
    {
        var services = new ServiceCollection();
        services.AddLocalEmbeddingsNpu(options =>
        {
            options.ModelName = "test-model";
            options.DeviceId = 1;
            options.EnsureModelDownloaded = false;
            options.ModelPath = @"C:\test";
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetService<Microsoft.Extensions.Options.IOptions<NpuEmbeddingsOptions>>();

        Assert.NotNull(options);
        Assert.Equal("test-model", options.Value.ModelName);
        Assert.Equal(1, options.Value.DeviceId);
    }

    [Fact]
    public void AddLocalEmbeddingsNpu_WithOptionsInstance_RegistersOptions()
    {
        var services = new ServiceCollection();
        var npuOptions = new NpuEmbeddingsOptions
        {
            ModelName = "custom-model",
            DeviceId = 2,
            EnsureModelDownloaded = false,
            ModelPath = @"C:\test"
        };
        services.AddLocalEmbeddingsNpu(npuOptions);

        var provider = services.BuildServiceProvider();
        var options = provider.GetService<Microsoft.Extensions.Options.IOptions<NpuEmbeddingsOptions>>();

        Assert.NotNull(options);
        Assert.Equal("custom-model", options.Value.ModelName);
        Assert.Equal(2, options.Value.DeviceId);
    }

    [Fact]
    public void AddLocalEmbeddingsNpu_WithModelName_RegistersOptions()
    {
        var services = new ServiceCollection();
        services.AddLocalEmbeddingsNpu("my-model");

        var provider = services.BuildServiceProvider();
        var options = provider.GetService<Microsoft.Extensions.Options.IOptions<NpuEmbeddingsOptions>>();

        Assert.NotNull(options);
        Assert.Equal("my-model", options.Value.ModelName);
    }

    [Fact]
    public void AddLocalEmbeddingsNpu_EmptyModelName_ThrowsArgumentException()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() => services.AddLocalEmbeddingsNpu(string.Empty));
    }

    [Fact]
    public void AddLocalEmbeddingsNpu_NullOptionsInstance_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() => services.AddLocalEmbeddingsNpu((NpuEmbeddingsOptions)null!));
    }

    [Fact]
    public void AddLocalEmbeddingsNpu_RegistersEmbeddingGenerator()
    {
        var services = new ServiceCollection();
        services.AddLocalEmbeddingsNpu(options =>
        {
            options.EnsureModelDownloaded = false;
            options.ModelPath = @"C:\test";
        });

        // Verify the service descriptor is registered
        var descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IEmbeddingGenerator<string, Embedding<float>>));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }
}
