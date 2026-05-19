using ElBruno.LocalEmbeddings.OpenTelemetry.Metrics;

namespace ElBruno.LocalEmbeddings.OpenTelemetry.Options;

/// <summary>
/// Configuration options for OpenTelemetry instrumentation.
/// </summary>
public sealed class LocalEmbeddingsOpenTelemetryOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether OpenTelemetry tracing is enabled.
    /// Default: true
    /// </summary>
    public bool EnableTracing { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether OpenTelemetry metrics collection is enabled.
    /// Default: true
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether W3C baggage propagation is enabled.
    /// Default: false
    /// </summary>
    public bool EnableBaggagePropagation { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether W3C baggage propagation is enabled.
    /// This property is an alias for <see cref="EnableBaggagePropagation"/>.
    /// </summary>
    public bool EnableBaggage
    {
        get => EnableBaggagePropagation;
        set => EnableBaggagePropagation = value;
    }

    /// <summary>
    /// Gets or sets the sampling rate (0.0 - 1.0).
    /// 0.0 = never sample, 1.0 = always sample.
    /// Default: 1.0 (sample all traces)
    /// </summary>
    public double SamplingRate { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets a value indicating whether to record detailed exception information in spans.
    /// Default: true
    /// </summary>
    public bool RecordExceptionDetails { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to record baggage items in span attributes.
    /// Default: false
    /// </summary>
    public bool RecordBaggageInAttributes { get; set; } = false;

    /// <summary>
    /// Gets or sets user-defined baggage items to include in activity tags.
    /// </summary>
    public IDictionary<string, string> BaggageItems { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the maximum number of baggage tags recorded per activity.
    /// Default: 16.
    /// </summary>
    public int MaxBaggageItemsToRecord { get; set; } = 16;

    /// <summary>
    /// Gets or sets the metric meter instance for recording metrics.
    /// If not set, a default instance will be created during initialization.
    /// </summary>
    public MetricMeter? MetricMeter { get; set; }

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if SamplingRate is outside the valid range [0, 1].</exception>
    public void Validate()
    {
        if (SamplingRate < 0.0 || SamplingRate > 1.0)
        {
            throw new ArgumentException($"SamplingRate must be between 0.0 and 1.0, got {SamplingRate}", nameof(SamplingRate));
        }

        if (MaxBaggageItemsToRecord < 0)
        {
            throw new ArgumentException($"MaxBaggageItemsToRecord must be greater than or equal to 0, got {MaxBaggageItemsToRecord}", nameof(MaxBaggageItemsToRecord));
        }
    }

    /// <summary>
    /// Determines whether tracing and metrics should be applied for the current request based on the sampling rate.
    /// </summary>
    /// <returns>true if the request should be sampled; otherwise false.</returns>
    public bool ShouldSample()
    {
        if (SamplingRate >= 1.0)
        {
            return true;
        }

        if (SamplingRate <= 0.0)
        {
            return false;
        }

        return Random.Shared.NextDouble() < SamplingRate;
    }
}
