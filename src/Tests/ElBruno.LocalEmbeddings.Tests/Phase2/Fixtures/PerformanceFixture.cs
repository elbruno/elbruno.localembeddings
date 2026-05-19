using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ElBruno.LocalEmbeddings.Tests.Phase2.Fixtures;

/// <summary>
/// Fixture for performance testing and baseline validation.
/// Records latency and memory metrics, compares against baselines.
/// Supports regression detection (fail if >10% worse, warn if 5-10% worse).
/// </summary>
public class PerformanceFixture : IAsyncLifetime
{
    private readonly string _baselineFilePath;
    private PerformanceBaseline? _baseline;
    private readonly Dictionary<string, long> _measurements = new();

    public PerformanceFixture()
    {
        _baselineFilePath = Path.Combine(AppContext.BaseDirectory, "performance-baseline.json");
    }

    public async Task InitializeAsync()
    {
        await LoadOrCreateBaseline();
    }

    public async Task DisposeAsync()
    {
        // Save any new measurements to baseline
        if (_measurements.Count > 0)
        {
            // Update baseline with new measurements if threshold exceeded
            await SaveBaseline();
        }

        _measurements.Clear();
        await Task.CompletedTask;
    }

    /// <summary>
    /// Records a latency measurement in milliseconds.
    /// </summary>
    public void RecordLatency(string operationName, long milliseconds)
    {
        var key = $"latency.{operationName}";
        _measurements[key] = milliseconds;
    }

    /// <summary>
    /// Records a memory measurement in bytes.
    /// </summary>
    public void RecordMemory(string operationName, long bytes)
    {
        var key = $"memory.{operationName}";
        _measurements[key] = bytes;
    }

    /// <summary>
    /// Records a throughput measurement (items per second).
    /// </summary>
    public void RecordThroughput(string operationName, long itemsPerSecond)
    {
        var key = $"throughput.{operationName}";
        _measurements[key] = itemsPerSecond;
    }

    /// <summary>
    /// Validates that a measurement is within acceptable bounds of baseline.
    /// Returns (isValid, percentageDifference, message)
    /// </summary>
    public (bool IsValid, double PercentageDifference, string Message) ValidateMeasurement(
        string operationName,
        long actualValue,
        bool isLowerBetter = true)
    {
        if (_baseline == null)
        {
            return (true, 0, "No baseline available");
        }

        if (!_baseline.Measurements.TryGetValue(operationName, out var baselineValue))
        {
            return (true, 0, $"No baseline for {operationName}");
        }

        if (baselineValue == 0)
        {
            return (true, 0, "Baseline value is zero");
        }

        double percentageDifference = isLowerBetter
            ? ((double)(actualValue - baselineValue) / baselineValue) * 100
            : ((double)(baselineValue - actualValue) / baselineValue) * 100;

        bool isValid = percentageDifference <= 10; // Allow up to 10% regression
        string level = percentageDifference > 10 ? "FAIL" : percentageDifference > 5 ? "WARN" : "OK";

        var message = $"{operationName}: {percentageDifference:F2}% difference ({level}) - baseline={baselineValue}, actual={actualValue}";

        return (isValid, percentageDifference, message);
    }

    /// <summary>
    /// Measure execution time of an async action.
    /// </summary>
    public async Task<long> MeasureAsync(string operationName, Func<Task> action)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await action();
        }
        finally
        {
            stopwatch.Stop();
            RecordLatency(operationName, stopwatch.ElapsedMilliseconds);
        }

        return stopwatch.ElapsedMilliseconds;
    }

    /// <summary>
    /// Measure execution time of an async function with return value.
    /// </summary>
    public async Task<(TResult Result, long ElapsedMilliseconds)> MeasureAsync<TResult>(
        string operationName,
        Func<Task<TResult>> action)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await action();
            return (result, stopwatch.ElapsedMilliseconds);
        }
        finally
        {
            stopwatch.Stop();
            RecordLatency(operationName, stopwatch.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// Measure memory usage using GC statistics.
    /// </summary>
    public long MeasureMemoryUsage()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return GC.GetTotalMemory(false);
    }

    private async Task LoadOrCreateBaseline()
    {
        if (File.Exists(_baselineFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_baselineFilePath);
                _baseline = JsonSerializer.Deserialize<PerformanceBaseline>(json);
            }
            catch
            {
                _baseline = CreateDefaultBaseline();
            }
        }
        else
        {
            _baseline = CreateDefaultBaseline();
        }
    }

    private async Task SaveBaseline()
    {
        if (_baseline == null)
            return;

        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var json = JsonSerializer.Serialize(_baseline, options);
            await File.WriteAllTextAsync(_baselineFilePath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }

    private static PerformanceBaseline CreateDefaultBaseline()
    {
        return new PerformanceBaseline
        {
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            Measurements = new Dictionary<string, long>
            {
                { "latency.generate_single", 10 },
                { "latency.generate_batch_100", 200 },
                { "latency.model_load_cold", 1000 },
                { "latency.model_load_cached", 50 },
                { "memory.model", 500_000_000 },
                { "throughput.streaming_100k", 10_000 },
            }
        };
    }

    public class PerformanceBaseline
    {
        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("measurements")]
        public Dictionary<string, long> Measurements { get; set; } = new();
    }
}
