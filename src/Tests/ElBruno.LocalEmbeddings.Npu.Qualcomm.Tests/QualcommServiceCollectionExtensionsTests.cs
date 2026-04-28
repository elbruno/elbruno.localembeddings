using ElBruno.LocalEmbeddings.Npu.Qualcomm.Extensions;
using ElBruno.LocalEmbeddings.Npu.Qualcomm.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace ElBruno.LocalEmbeddings.Npu.Qualcomm.Tests;

public class QualcommServiceCollectionExtensionsTests
{
    [Fact]
    public void AddLocalEmbeddingsNpuQualcomm_RegistersOptions()
    {
        var services = new ServiceCollection();
        services.AddLocalEmbeddingsNpuQualcomm(options =>
        {
            options.ModelName = "test-model";
            options.QnnBackendPath = "QnnCpu.dll";
            options.EnsureModelDownloaded = false;
            options.ModelPath = @"C:\test";
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetService<Microsoft.Extensions.Options.IOptions<QualcommEmbeddingsOptions>>();

        Assert.NotNull(options);
        Assert.Equal("test-model", options.Value.ModelName);
        Assert.Equal("QnnCpu.dll", options.Value.QnnBackendPath);
    }

    [Fact]
    public void AddLocalEmbeddingsNpuQualcomm_WithOptionsInstance_RegistersOptions()
    {
        var services = new ServiceCollection();
        var qcOptions = new QualcommEmbeddingsOptions
        {
            ModelName = "custom-model",
            QnnBackendPath = "QnnHtp.dll",
            EnsureModelDownloaded = false,
            ModelPath = @"C:\test"
        };
        services.AddLocalEmbeddingsNpuQualcomm(qcOptions);

        var provider = services.BuildServiceProvider();
        var options = provider.GetService<Microsoft.Extensions.Options.IOptions<QualcommEmbeddingsOptions>>();

        Assert.NotNull(options);
        Assert.Equal("custom-model", options.Value.ModelName);
        Assert.Equal("QnnHtp.dll", options.Value.QnnBackendPath);
    }

    [Fact]
    public void AddLocalEmbeddingsNpuQualcomm_WithModelName_RegistersOptions()
    {
        var services = new ServiceCollection();
        services.AddLocalEmbeddingsNpuQualcomm("my-model");

        var provider = services.BuildServiceProvider();
        var options = provider.GetService<Microsoft.Extensions.Options.IOptions<QualcommEmbeddingsOptions>>();

        Assert.NotNull(options);
        Assert.Equal("my-model", options.Value.ModelName);
    }

    [Fact]
    public void AddLocalEmbeddingsNpuQualcomm_EmptyModelName_ThrowsArgumentException()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() => services.AddLocalEmbeddingsNpuQualcomm(string.Empty));
    }

    [Fact]
    public void AddLocalEmbeddingsNpuQualcomm_NullOptionsInstance_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() => services.AddLocalEmbeddingsNpuQualcomm((QualcommEmbeddingsOptions)null!));
    }

    [Fact]
    public void AddLocalEmbeddingsNpuQualcomm_RegistersEmbeddingGenerator()
    {
        var services = new ServiceCollection();
        services.AddLocalEmbeddingsNpuQualcomm(options =>
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
