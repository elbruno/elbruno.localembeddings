using ElBruno.LocalEmbeddings.Harrier.Extensions;
using ElBruno.LocalEmbeddings.Harrier.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ElBruno.LocalEmbeddings.Harrier.Tests;

public class HarrierDIExtensionsTests
{
    [Fact]
    public void AddHarrierEmbeddings_WithAction_RegistersOptions()
    {
        var services = new ServiceCollection();

        services.AddHarrierEmbeddings(options =>
        {
            options.MaxSequenceLength = 4096;
            options.InstructionPrefix = "custom: ";
        });

        using var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<IOptions<HarrierEmbeddingsOptions>>().Value;

        Assert.Equal(4096, resolved.MaxSequenceLength);
        Assert.Equal("custom: ", resolved.InstructionPrefix);
    }

    [Fact]
    public void AddHarrierEmbeddings_WithNullAction_DoesNotThrow()
    {
        var services = new ServiceCollection();

        // null configure action is allowed — uses defaults
        services.AddHarrierEmbeddings(configure: null);

        using var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<IOptions<HarrierEmbeddingsOptions>>().Value;

        Assert.Equal(HarrierEmbeddingsOptions.DefaultModelName, resolved.ModelName);
    }

    [Fact]
    public void AddHarrierEmbeddings_WithConfiguration_BindsValues()
    {
        var services = new ServiceCollection();

        // Build an IConfiguration using ConfigurationBuilder + key-value pairs
        var configData = new Dictionary<string, string?>
        {
            ["MaxSequenceLength"] = "2048",
            ["ModelName"] = "custom/model",
            ["EnsureModelDownloaded"] = "false"
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        services.AddHarrierEmbeddings(config);

        using var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<IOptions<HarrierEmbeddingsOptions>>().Value;

        Assert.Equal(2048, resolved.MaxSequenceLength);
        Assert.Equal("custom/model", resolved.ModelName);
        Assert.False(resolved.EnsureModelDownloaded);
    }

    [Fact]
    public void AddHarrierEmbeddings_WithNullOptionsInstance_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
            services.AddHarrierEmbeddings((HarrierEmbeddingsOptions)null!));
    }

    [Fact]
    public void AddHarrierEmbeddings_WithNullConfiguration_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
            services.AddHarrierEmbeddings((IConfiguration)null!));
    }

    [Fact]
    public void AddHarrierEmbeddings_RegistersEmbeddingGeneratorService()
    {
        var services = new ServiceCollection();

        services.AddHarrierEmbeddings(options =>
        {
            options.EnsureModelDownloaded = false;
            options.ModelPath = "unused-for-registration";
        });

        Assert.Contains(services, s =>
            s.ServiceType == typeof(IEmbeddingGenerator<string, Embedding<float>>));
    }
}
