using System.Collections.Concurrent;
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
        Assert.Null(metadata.DefaultModelId);
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

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task GenerateAsync_BaggageConfiguration_CompletesSuccessfully(
        bool enableBaggagePropagation,
        bool recordBaggageInAttributes)
    {
        using var baggageScope = new ParentActivityScope(("trace.request_id", "req-123"), ("trace.user_id", "user-123"));

        var embeddings = new[] { new Embedding<float>(new float[] { 1.0f, 2.0f, 3.0f }) };
        var expectedResult = new GeneratedEmbeddings<Embedding<float>>(embeddings);

        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        mockGenerator.Setup(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), null, default))
            .ReturnsAsync(expectedResult);
        var options = new LocalEmbeddingsOpenTelemetryOptions
        {
            EnableTracing = true,
            EnableBaggagePropagation = enableBaggagePropagation,
            RecordBaggageInAttributes = recordBaggageInAttributes,
            EnableMetrics = false
        };

        var instrumentedGenerator = new InstrumentedEmbeddingGenerator(mockGenerator.Object, options);

        var result = await instrumentedGenerator.GenerateAsync(new[] { "test" });

        Assert.Single(result);
    }

    [Fact]
    public async Task GenerateAsync_BaggageEnabledAndRecorded_AddsBaggageTags()
    {
        var completedActivities = new ConcurrentQueue<Activity>();
        using var listener = CreateListener(completedActivities);

        var embeddings = new[] { new Embedding<float>(new float[] { 1.0f, 2.0f, 3.0f }) };
        var expectedResult = new GeneratedEmbeddings<Embedding<float>>(embeddings);

        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        mockGenerator.Setup(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), null, default))
            .ReturnsAsync(expectedResult);

        var baggageProvider = new DictionaryBaggageProvider(new Dictionary<string, string?>
        {
            ["trace.request_id"] = "req-123",
            ["trace.user_id"] = "user-123"
        });

        var options = new LocalEmbeddingsOpenTelemetryOptions
        {
            EnableTracing = true,
            EnableMetrics = false,
            EnableBaggagePropagation = true,
            RecordBaggageInAttributes = true
        };

        var instrumentedGenerator = new InstrumentedEmbeddingGenerator(
            mockGenerator.Object,
            options,
            baggageProvider);

        _ = await instrumentedGenerator.GenerateAsync(new[] { "test" });

        var recordedActivities = completedActivities.ToArray();
        Assert.Contains(recordedActivities, a => Equals(a.GetTagItem("baggage.trace.request_id"), "req-123"));
        Assert.Contains(recordedActivities, a => Equals(a.GetTagItem("baggage.trace.user_id"), "user-123"));
    }

    private static ActivityListener CreateListener(ConcurrentQueue<Activity> completedActivities)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "ElBruno.LocalEmbeddings",
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => completedActivities.Enqueue(activity)
        };

        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private sealed class ParentActivityScope : IDisposable
    {
        private readonly Activity _activity;

        public ParentActivityScope(params (string Key, string Value)[] items)
        {
            _activity = new Activity("test-parent");
            foreach (var (key, value) in items)
            {
                _activity.AddBaggage(key, value);
            }

            _activity.Start();
        }

        public void Dispose()
        {
            _activity.Stop();
        }
    }

    private sealed class DictionaryBaggageProvider : IActivityBaggageProvider
    {
        private readonly IDictionary<string, string?> _items;

        public DictionaryBaggageProvider(IDictionary<string, string?> items)
        {
            _items = items;
        }

        public IEnumerable<KeyValuePair<string, string?>> GetBaggage() => _items;

        public void SetBaggage(string key, string? value) => _items[key] = value;

        public bool TryReadFromHeader(string? baggageHeader) => BaggageExtensions.TryReadFromHeader(baggageHeader, this);
    }
}
