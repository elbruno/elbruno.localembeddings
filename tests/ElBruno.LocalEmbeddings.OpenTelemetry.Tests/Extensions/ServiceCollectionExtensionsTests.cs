using ElBruno.LocalEmbeddings.OpenTelemetry.Extensions;
using ElBruno.LocalEmbeddings.OpenTelemetry.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ElBruno.LocalEmbeddings.OpenTelemetry.Tests.Extensions;

/// <summary>
/// Unit tests for ServiceCollectionExtensions.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddLocalEmbeddingsOpenTelemetry_ThrowsArgumentNullException_WhenServicesIsNull()
    {
        IServiceCollection services = null!;
        
        Assert.Throws<ArgumentNullException>(() =>
            services.AddLocalEmbeddingsOpenTelemetry());
    }

    [Fact]
    public void AddLocalEmbeddingsOpenTelemetry_ThrowsInvalidOperationException_WhenGeneratorNotRegistered()
    {
        var services = new ServiceCollection();
        
        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddLocalEmbeddingsOpenTelemetry());
        
        Assert.Contains("No IEmbeddingGenerator<string, Embedding<float>> is registered", ex.Message);
    }

    [Fact]
    public void AddLocalEmbeddingsOpenTelemetry_Succeeds_WhenGeneratorIsRegistered()
    {
        var services = new ServiceCollection();
        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        services.AddSingleton(mockGenerator.Object);

        services.AddLocalEmbeddingsOpenTelemetry();

        var serviceProvider = services.BuildServiceProvider();
        var generator = serviceProvider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

        Assert.NotNull(generator);
    }

    [Fact]
    public void AddLocalEmbeddingsOpenTelemetry_WithOptions_Succeeds()
    {
        var services = new ServiceCollection();
        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        services.AddSingleton(mockGenerator.Object);

        var options = new LocalEmbeddingsOpenTelemetryOptions { SamplingRate = 0.5 };
        services.AddLocalEmbeddingsOpenTelemetry(options);

        var serviceProvider = services.BuildServiceProvider();
        var generator = serviceProvider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        var resolvedOptions = serviceProvider.GetRequiredService<LocalEmbeddingsOpenTelemetryOptions>();

        Assert.NotNull(generator);
        Assert.Equal(0.5, resolvedOptions.SamplingRate);
    }

    [Fact]
    public void AddLocalEmbeddingsOpenTelemetry_WithConfigure_Succeeds()
    {
        var services = new ServiceCollection();
        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        services.AddSingleton(mockGenerator.Object);

        services.AddLocalEmbeddingsOpenTelemetry(opts => opts.SamplingRate = 0.3);

        var serviceProvider = services.BuildServiceProvider();
        var resolvedOptions = serviceProvider.GetRequiredService<LocalEmbeddingsOpenTelemetryOptions>();

        Assert.Equal(0.3, resolvedOptions.SamplingRate);
    }

    [Fact]
    public void AddLocalEmbeddingsOpenTelemetry_WithInvalidOptions_Throws()
    {
        var services = new ServiceCollection();
        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        services.AddSingleton(mockGenerator.Object);

        var invalidOptions = new LocalEmbeddingsOpenTelemetryOptions { SamplingRate = 1.5 };

        Assert.Throws<ArgumentException>(() =>
            services.AddLocalEmbeddingsOpenTelemetry(invalidOptions));
    }
}
