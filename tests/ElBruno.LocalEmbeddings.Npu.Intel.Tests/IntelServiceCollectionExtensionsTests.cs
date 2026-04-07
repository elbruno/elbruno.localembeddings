using ElBruno.LocalEmbeddings.Npu.Intel.Extensions;
using ElBruno.LocalEmbeddings.Npu.Intel.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace ElBruno.LocalEmbeddings.Npu.Intel.Tests;

public class IntelServiceCollectionExtensionsTests
{
    [Fact]
    public void AddLocalEmbeddingsNpuIntel_RegistersOptions()
    {
        var services = new ServiceCollection();
        services.AddLocalEmbeddingsNpuIntel(options =>
        {
            options.ModelName = "test-model";
            options.DeviceType = "CPU";
            options.EnsureModelDownloaded = false;
            options.ModelPath = @"C:\test";
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetService<Microsoft.Extensions.Options.IOptions<IntelEmbeddingsOptions>>();

        Assert.NotNull(options);
        Assert.Equal("test-model", options.Value.ModelName);
        Assert.Equal("CPU", options.Value.DeviceType);
    }

    [Fact]
    public void AddLocalEmbeddingsNpuIntel_WithOptionsInstance_RegistersOptions()
    {
        var services = new ServiceCollection();
        var intelOptions = new IntelEmbeddingsOptions
        {
            ModelName = "custom-model",
            DeviceType = "NPU",
            EnsureModelDownloaded = false,
            ModelPath = @"C:\test"
        };
        services.AddLocalEmbeddingsNpuIntel(intelOptions);

        var provider = services.BuildServiceProvider();
        var options = provider.GetService<Microsoft.Extensions.Options.IOptions<IntelEmbeddingsOptions>>();

        Assert.NotNull(options);
        Assert.Equal("custom-model", options.Value.ModelName);
        Assert.Equal("NPU", options.Value.DeviceType);
    }

    [Fact]
    public void AddLocalEmbeddingsNpuIntel_WithModelName_RegistersOptions()
    {
        var services = new ServiceCollection();
        services.AddLocalEmbeddingsNpuIntel("my-model");

        var provider = services.BuildServiceProvider();
        var options = provider.GetService<Microsoft.Extensions.Options.IOptions<IntelEmbeddingsOptions>>();

        Assert.NotNull(options);
        Assert.Equal("my-model", options.Value.ModelName);
    }

    [Fact]
    public void AddLocalEmbeddingsNpuIntel_EmptyModelName_ThrowsArgumentException()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() => services.AddLocalEmbeddingsNpuIntel(string.Empty));
    }

    [Fact]
    public void AddLocalEmbeddingsNpuIntel_NullOptionsInstance_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() => services.AddLocalEmbeddingsNpuIntel((IntelEmbeddingsOptions)null!));
    }

    [Fact]
    public void AddLocalEmbeddingsNpuIntel_RegistersEmbeddingGenerator()
    {
        var services = new ServiceCollection();
        services.AddLocalEmbeddingsNpuIntel(options =>
        {
            options.EnsureModelDownloaded = false;
            options.ModelPath = @"C:\test";
        });

        var descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IEmbeddingGenerator<string, Embedding<float>>));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }
}
