using ElBruno.LocalEmbeddings.Extensions;
using ElBruno.LocalEmbeddings.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ElBruno.LocalEmbeddings.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddLocalEmbeddings_WithConfigureAction_RegistersExpectedServices()
    {
        var services = new ServiceCollection();

        services.AddLocalEmbeddings(options =>
        {
            options.ModelName = "test/model";
            options.MaxSequenceLength = 123;
        });

        Assert.Contains(services, s => s.ServiceType == typeof(IModelDownloader));
        Assert.Contains(services, s => s.ServiceType == typeof(IEmbeddingGenerator<string, Embedding<float>>));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<LocalEmbeddingsOptions>>().Value;
        Assert.Equal("test/model", options.ModelName);
        Assert.Equal(123, options.MaxSequenceLength);
    }

    [Fact]
    public void AddLocalEmbeddings_WithOptionsInstance_UsesProvidedOptions()
    {
        var services = new ServiceCollection();
        var input = new LocalEmbeddingsOptions
        {
            ModelName = "sample/model",
            EnsureModelDownloaded = false,
            ModelPath = "c:/models/sample",
            MaxSequenceLength = 64,
            NormalizeEmbeddings = true,
            UseParallelExecution = false,
            PreferQuantized = true,
            InterOpNumThreads = 2,
            IntraOpNumThreads = 1
        };

        services.AddLocalEmbeddings(input);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<LocalEmbeddingsOptions>>().Value;

        Assert.Equal("sample/model", options.ModelName);
        Assert.False(options.EnsureModelDownloaded);
        Assert.Equal("c:/models/sample", options.ModelPath);
        Assert.Equal(64, options.MaxSequenceLength);
        Assert.True(options.NormalizeEmbeddings);
        Assert.False(options.UseParallelExecution);
        Assert.True(options.PreferQuantized);
        Assert.Equal(2, options.InterOpNumThreads);
        Assert.Equal(1, options.IntraOpNumThreads);
    }

    [Fact]
    public void AddLocalEmbeddings_WithModelName_UsesModelName()
    {
        var services = new ServiceCollection();

        services.AddLocalEmbeddings("sentence-transformers/test-model");

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<LocalEmbeddingsOptions>>().Value;
        Assert.Equal("sentence-transformers/test-model", options.ModelName);
    }

    [Fact]
    public void AddLocalEmbeddings_WithConfiguration_BindsConfigurationValues()
    {
        var data = new Dictionary<string, string?>
        {
            ["LocalEmbeddings:ModelName"] = "cfg/model",
            ["LocalEmbeddings:MaxSequenceLength"] = "321",
            ["LocalEmbeddings:EnsureModelDownloaded"] = "false"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build()
            .GetSection("LocalEmbeddings");

        var services = new ServiceCollection();
        services.AddLocalEmbeddings(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<LocalEmbeddingsOptions>>().Value;
        Assert.Equal("cfg/model", options.ModelName);
        Assert.Equal(321, options.MaxSequenceLength);
        Assert.False(options.EnsureModelDownloaded);
    }

    [Fact]
    public void AddLocalEmbeddings_WithInvalidModelName_ThrowsArgumentException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddLocalEmbeddings("  "));
    }

    [Fact]
    public void AddLocalEmbeddings_WithNullOptions_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddLocalEmbeddings((LocalEmbeddingsOptions)null!));
    }

    [Fact]
    public void AddLocalEmbeddings_WithNullConfiguration_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddLocalEmbeddings((IConfiguration)null!));
    }
}
