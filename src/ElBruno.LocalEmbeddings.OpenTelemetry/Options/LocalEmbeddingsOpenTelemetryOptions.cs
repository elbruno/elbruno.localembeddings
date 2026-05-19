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
    /// Validates the configuration options.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if SamplingRate is outside the valid range [0, 1].</exception>
    public void Validate()
    {
        if (SamplingRate < 0.0 || SamplingRate > 1.0)
        {
            throw new ArgumentException($"SamplingRate must be between 0.0 and 1.0, got {SamplingRate}", nameof(SamplingRate));
        }
    }
}
