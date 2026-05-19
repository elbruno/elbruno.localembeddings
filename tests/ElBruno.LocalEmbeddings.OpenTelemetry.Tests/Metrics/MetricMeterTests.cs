using ElBruno.LocalEmbeddings.OpenTelemetry.Metrics;
using Xunit;

namespace ElBruno.LocalEmbeddings.OpenTelemetry.Tests.Metrics;

/// <summary>
/// Unit tests for MetricMeter.
/// </summary>
public class MetricMeterTests
{
    [Fact]
    public void Constructor_CreatesAllMetrics()
    {
        using var meter = new MetricMeter();
        
        // Verify the meter is created (indirectly tested through recording operations)
        Assert.NotNull(meter.GetMeter());
    }

    [Fact]
    public void OTEL_Metrics_All_Registered_HistogramsAreRecordable()
    {
        using var meter = new MetricMeter();
        
        // Verify histograms can record without throwing
        meter.RecordEmbeddingLatency(100.0);
        meter.RecordModelLoadLatency(50.0);
        meter.RecordQuantizationCheckLatency(25.0);
        meter.RecordBatchSize(10);
    }

    [Fact]
    public void OTEL_Metrics_All_Registered_CountersAreIncmentable()
    {
        using var meter = new MetricMeter();
        
        // Verify counters can increment without throwing
        meter.IncrementEmbeddingsGenerated(5);
        meter.IncrementModelsLoaded();
        meter.IncrementErrors();
        meter.IncrementCacheHits();
        meter.IncrementCacheMisses();
    }

    [Fact]
    public void OTEL_Metrics_All_Registered_GaugesAreSettable()
    {
        using var meter = new MetricMeter();
        
        meter.SetActiveRequests(10);
        meter.SetModelCacheSizeMb(512);
        
        Assert.Equal(10, meter.GetActiveRequests());
        Assert.Equal(512, meter.GetModelCacheSizeMb());
    }

    [Fact]
    public void RecordEmbeddingLatency_WithTags()
    {
        using var meter = new MetricMeter();
        var tags = new[] { new KeyValuePair<string, object?>("model", "test-model") };
        
        meter.RecordEmbeddingLatency(100.0, tags);
        // Test passes if no exception is thrown
    }

    [Fact]
    public void RecordModelLoadLatency_WithTags()
    {
        using var meter = new MetricMeter();
        var tags = new[] { new KeyValuePair<string, object?>("format", "onnx") };
        
        meter.RecordModelLoadLatency(50.0, tags);
        // Test passes if no exception is thrown
    }

    [Fact]
    public void RecordQuantizationCheckLatency_WithTags()
    {
        using var meter = new MetricMeter();
        var tags = new[] { new KeyValuePair<string, object?>("status", "success") };
        
        meter.RecordQuantizationCheckLatency(25.0, tags);
        // Test passes if no exception is thrown
    }

    [Fact]
    public void RecordBatchSize_WithTags()
    {
        using var meter = new MetricMeter();
        var tags = new[] { new KeyValuePair<string, object?>("source", "api") };
        
        meter.RecordBatchSize(32, tags);
        // Test passes if no exception is thrown
    }

    [Fact]
    public void IncrementCounters_WithTags()
    {
        using var meter = new MetricMeter();
        var tags = new[] { new KeyValuePair<string, object?>("status", "success") };
        
        meter.IncrementEmbeddingsGenerated(10, tags);
        meter.IncrementModelsLoaded(1, tags);
        meter.IncrementErrors(0, tags);
        meter.IncrementCacheHits(5, tags);
        meter.IncrementCacheMisses(2, tags);
        // Test passes if no exception is thrown
    }

    [Fact]
    public void IncrementEmbeddingsGenerated_MultipleValues()
    {
        using var meter = new MetricMeter();
        
        meter.IncrementEmbeddingsGenerated(100);
        meter.IncrementEmbeddingsGenerated(50);
        meter.IncrementEmbeddingsGenerated(25);
        // Test passes if no exception is thrown
    }

    [Fact]
    public void SetActiveRequests_UpdatesValue()
    {
        using var meter = new MetricMeter();
        
        meter.SetActiveRequests(5);
        Assert.Equal(5, meter.GetActiveRequests());
        
        meter.SetActiveRequests(10);
        Assert.Equal(10, meter.GetActiveRequests());
    }

    [Fact]
    public void SetModelCacheSizeMb_UpdatesValue()
    {
        using var meter = new MetricMeter();
        
        meter.SetModelCacheSizeMb(256);
        Assert.Equal(256, meter.GetModelCacheSizeMb());
        
        meter.SetModelCacheSizeMb(512);
        Assert.Equal(512, meter.GetModelCacheSizeMb());
    }

    [Fact]
    public void Dispose_PreventsFutureRecording()
    {
        var meter = new MetricMeter();
        meter.Dispose();
        
        Assert.Throws<ObjectDisposedException>(() => meter.RecordEmbeddingLatency(100.0));
    }

    [Fact]
    public void Dispose_MultipleCalls_DoesNotThrow()
    {
        var meter = new MetricMeter();
        meter.Dispose();
        meter.Dispose(); // Should not throw
    }

    [Fact]
    public void GetMeter_ReturnsUnderlyingMeter()
    {
        using var meter = new MetricMeter();
        
        var underlying = meter.GetMeter();
        Assert.NotNull(underlying);
    }
}
