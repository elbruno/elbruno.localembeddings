using System.Diagnostics;
using ElBruno.LocalEmbeddings.OpenTelemetry.Internal;
using ElBruno.LocalEmbeddings.OpenTelemetry.Instrumentation;
using ElBruno.LocalEmbeddings.OpenTelemetry.Options;
using Microsoft.Extensions.AI;
using Moq;
using Xunit;

namespace ElBruno.LocalEmbeddings.OpenTelemetry.Tests.Instrumentation;

/// <summary>
/// Unit tests for InstrumentedEmbeddingGenerator.
/// </summary>
public class InstrumentedEmbeddingGeneratorTests
{
    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenInnerGeneratorIsNull()
    {
        var options = new LocalEmbeddingsOpenTelemetryOptions();
        
        Assert.Throws<ArgumentNullException>(() =>
            new InstrumentedEmbeddingGenerator(null!, options));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenOptionsIsNull()
    {
        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        
        Assert.Throws<ArgumentNullException>(() =>
            new InstrumentedEmbeddingGenerator(mockGenerator.Object, null!));
    }

    [Fact]
    public void Constructor_ValidatesOptions()
    {
        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        var invalidOptions = new LocalEmbeddingsOpenTelemetryOptions { SamplingRate = 1.5 };
        
        Assert.Throws<ArgumentException>(() =>
            new InstrumentedEmbeddingGenerator(mockGenerator.Object, invalidOptions));
    }

    [Fact]
    public void Metadata_ReturnsDefaultMetadata_WhenInnerGeneratorHasNoMetadata()
    {
        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        mockGenerator.Setup(g => g.GetService(It.IsAny<Type>(), null))
            .Returns(null as EmbeddingGeneratorMetadata);

        var options = new LocalEmbeddingsOpenTelemetryOptions { EnableTracing = false };
        var instrumentedGenerator = new InstrumentedEmbeddingGenerator(mockGenerator.Object, options);

        var metadata = instrumentedGenerator.Metadata;
        
        Assert.NotNull(metadata);
        Assert.Equal("InstrumentedLocalEmbeddings", metadata.DefaultModelId);
    }

    [Fact]
    public void Metadata_ReturnsInnerMetadata_WhenInnerGeneratorHasMetadata()
    {
        var innerMetadata = new EmbeddingGeneratorMetadata("test-model");
        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        mockGenerator.Setup(g => g.GetService(It.IsAny<Type>(), null))
            .Returns((Type t, object? k) => t == typeof(EmbeddingGeneratorMetadata) ? innerMetadata : null);

        var options = new LocalEmbeddingsOpenTelemetryOptions { EnableTracing = false };
        var instrumentedGenerator = new InstrumentedEmbeddingGenerator(mockGenerator.Object, options);

        var metadata = instrumentedGenerator.Metadata;
        
        Assert.NotNull(metadata);
        Assert.Equal("test-model", metadata.DefaultModelId);
    }

    [Fact]
    public async Task GenerateAsync_PassesThrough_WhenTracingIsDisabled()
    {
        var embeddings = new[] { new Embedding<float>(new float[] { 1.0f, 2.0f }) };
        var expectedResult = new GeneratedEmbeddings<Embedding<float>>(embeddings);

        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        mockGenerator.Setup(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), null, default))
            .ReturnsAsync(expectedResult);

        var options = new LocalEmbeddingsOpenTelemetryOptions { EnableTracing = false };
        var instrumentedGenerator = new InstrumentedEmbeddingGenerator(mockGenerator.Object, options);

        var result = await instrumentedGenerator.GenerateAsync(new[] { "test" });

        Assert.Equal(embeddings.Length, result.Count);
        mockGenerator.Verify(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), null, default), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_ThrowsObjectDisposedException_WhenDisposed()
    {
        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        var options = new LocalEmbeddingsOpenTelemetryOptions();
        var instrumentedGenerator = new InstrumentedEmbeddingGenerator(mockGenerator.Object, options);

        instrumentedGenerator.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            instrumentedGenerator.GenerateAsync(new[] { "test" }));
    }

    [Fact]
    public async Task DisposeAsync_DisposesInnerGenerator_WhenItIsAsyncDisposable()
    {
        var mockAsyncDisposable = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        mockAsyncDisposable.As<IAsyncDisposable>();

        var options = new LocalEmbeddingsOpenTelemetryOptions();
        var instrumentedGenerator = new InstrumentedEmbeddingGenerator(mockAsyncDisposable.Object, options);

        await instrumentedGenerator.DisposeAsync();

        mockAsyncDisposable.As<IAsyncDisposable>().Verify(d => d.DisposeAsync(), Times.Once);
    }

    [Fact]
    public void GetService_DelegatesToInnerGenerator()
    {
        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        var serviceType = typeof(EmbeddingGeneratorMetadata);
        mockGenerator.Setup(g => g.GetService(serviceType, null))
            .Returns(new EmbeddingGeneratorMetadata("test"));

        var options = new LocalEmbeddingsOpenTelemetryOptions();
        var instrumentedGenerator = new InstrumentedEmbeddingGenerator(mockGenerator.Object, options);

        var service = instrumentedGenerator.GetService(serviceType, null);

        Assert.NotNull(service);
        mockGenerator.Verify(g => g.GetService(serviceType, null), Times.Once);
    }
}
