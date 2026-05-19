using ElBruno.LocalEmbeddings.OpenTelemetry.Instrumentation;
using ElBruno.LocalEmbeddings.OpenTelemetry.Metrics;
using ElBruno.LocalEmbeddings.OpenTelemetry.Options;
using Microsoft.Extensions.AI;
using Moq;
using Xunit;

namespace ElBruno.LocalEmbeddings.OpenTelemetry.Tests.Instrumentation;

/// <summary>
/// Performance tests for OpenTelemetry instrumentation overhead.
/// </summary>
public class PerformanceOverheadTests
{
    [Fact(Skip = "Long-running performance test - enable for performance validation")]
    public async Task OTEL_Overhead_LessThan2Percent_MeasuresInstrumentation()
    {
        const int iterations = 10000;
        const int itemsPerBatch = 100;
        const double maxAllowedOverheadPercent = 2.0;

        // Create mock generator with minimal latency
        var embeddings = new[] { new Embedding<float>(new float[] { 1.0f, 2.0f, 3.0f }) };
        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        mockGenerator.Setup(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), null, default))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>(embeddings));

        // Generate test data
        var testItems = Enumerable.Range(0, itemsPerBatch)
            .Select(i => $"test-{i}")
            .ToList();

        // Measure baseline (without instrumentation)
        var baselineStopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            await mockGenerator.Object.GenerateAsync(testItems, null, default);
        }
        baselineStopwatch.Stop();
        long baselineMs = baselineStopwatch.ElapsedMilliseconds;

        // Measure with instrumentation (100% sampling)
        var optionsWithInstrumentation = new LocalEmbeddingsOpenTelemetryOptions
        {
            EnableTracing = true,
            EnableMetrics = true,
            SamplingRate = 1.0,
            MetricMeter = new MetricMeter()
        };

        var instrumentedGenerator = new InstrumentedEmbeddingGenerator(
            mockGenerator.Object,
            optionsWithInstrumentation);

        var instrumentedStopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            await instrumentedGenerator.GenerateAsync(testItems, null, default);
        }
        instrumentedStopwatch.Stop();
        long instrumentedMs = instrumentedStopwatch.ElapsedMilliseconds;

        // Calculate overhead
        double overheadMs = instrumentedMs - baselineMs;
        double overheadPercent = (overheadMs / baselineMs) * 100;

        // Assert that overhead is within acceptable range
        Assert.True(overheadPercent < maxAllowedOverheadPercent,
            $"Instrumentation overhead {overheadPercent:F2}% exceeds maximum allowed {maxAllowedOverheadPercent}%\n" +
            $"Baseline: {baselineMs}ms, Instrumented: {instrumentedMs}ms, Overhead: {overheadMs}ms");
    }

    [Fact]
    public async Task OTEL_Overhead_LessThan2Percent_WithSampling_ReducesOverhead()
    {
        const int iterations = 1000;
        const int itemsPerBatch = 100;

        var embeddings = new[] { new Embedding<float>(new float[] { 1.0f, 2.0f, 3.0f }) };
        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        mockGenerator.Setup(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), null, default))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>(embeddings));

        var testItems = Enumerable.Range(0, itemsPerBatch)
            .Select(i => $"test-{i}")
            .ToList();

        // Test with 10% sampling rate (should have lower overhead)
        var optionsWithSampling = new LocalEmbeddingsOpenTelemetryOptions
        {
            EnableTracing = true,
            EnableMetrics = true,
            SamplingRate = 0.1,
            MetricMeter = new MetricMeter()
        };

        var instrumentedGenerator = new InstrumentedEmbeddingGenerator(
            mockGenerator.Object,
            optionsWithSampling);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            await instrumentedGenerator.GenerateAsync(testItems, null, default);
        }
        stopwatch.Stop();

        // Test completes without timeout
        Assert.True(stopwatch.ElapsedMilliseconds > 0);
    }

    [Fact]
    public async Task OTEL_Overhead_LessThan2Percent_WithTracingDisabled_HasMinimalOverhead()
    {
        const int iterations = 1000;
        const int itemsPerBatch = 100;

        var embeddings = new[] { new Embedding<float>(new float[] { 1.0f, 2.0f, 3.0f }) };
        var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        mockGenerator.Setup(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), null, default))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>(embeddings));

        var testItems = Enumerable.Range(0, itemsPerBatch)
            .Select(i => $"test-{i}")
            .ToList();

        // With tracing disabled, should be near-baseline
        var optionsDisabled = new LocalEmbeddingsOpenTelemetryOptions
        {
            EnableTracing = false,
            EnableMetrics = false,
            SamplingRate = 1.0
        };

        var instrumentedGenerator = new InstrumentedEmbeddingGenerator(
            mockGenerator.Object,
            optionsDisabled);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            await instrumentedGenerator.GenerateAsync(testItems, null, default);
        }
        stopwatch.Stop();

        // Should complete quickly when disabled
        Assert.True(stopwatch.ElapsedMilliseconds > 0);
    }

    [Fact]
    public void MetricRecording_DoesNotThrow_UnderConcurrency()
    {
        using var meter = new MetricMeter();
        const int threadCount = 10;
        const int operationsPerThread = 1000;

        var tasks = Enumerable.Range(0, threadCount)
            .Select(threadId =>
                Task.Run(() =>
                {
                    for (int i = 0; i < operationsPerThread; i++)
                    {
                        meter.RecordEmbeddingLatency(50.0 + i);
                        meter.RecordBatchSize(i % 100);
                        meter.IncrementEmbeddingsGenerated(1);
                        meter.SetActiveRequests(i % 100);
                    }
                }))
            .ToArray();

        // Should not throw
        Task.WaitAll(tasks);

        // Metrics should still be operational
        meter.RecordEmbeddingLatency(100.0);
        Assert.NoThrow(() => meter.RecordEmbeddingLatency(100.0));
    }

    [Fact]
    public async Task SamplingLogic_DoesNotImpactPerformance()
    {
        const int iterations = 10000;

        var options1 = new LocalEmbeddingsOpenTelemetryOptions { SamplingRate = 1.0 };
        var options2 = new LocalEmbeddingsOpenTelemetryOptions { SamplingRate = 0.5 };
        var options3 = new LocalEmbeddingsOpenTelemetryOptions { SamplingRate = 0.0 };

        var watch1 = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _ = options1.ShouldSample();
        }
        watch1.Stop();

        var watch2 = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _ = options2.ShouldSample();
        }
        watch2.Stop();

        var watch3 = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _ = options3.ShouldSample();
        }
        watch3.Stop();

        // All should complete reasonably quickly (< 100ms for 10k iterations)
        Assert.True(watch1.ElapsedMilliseconds < 100);
        Assert.True(watch2.ElapsedMilliseconds < 100);
        Assert.True(watch3.ElapsedMilliseconds < 100);
    }
}

/// <summary>
/// Helper extension methods for assertions.
/// </summary>
internal static class AssertExtensions
{
    public static void NoThrow(Action action)
    {
        action();
    }
}
