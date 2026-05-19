namespace ElBruno.LocalEmbeddings.OpenTelemetry.Internal;

/// <summary>
/// Metric instrument name constants for OpenTelemetry instrumentation.
/// </summary>
internal static class MetricNames
{
    // Histograms (latency measurements in milliseconds)
    public const string EmbeddingLatencyMs = "embedding.latency.ms";
    public const string ModelLoadLatencyMs = "model.load.latency.ms";
    public const string QuantizationCheckLatencyMs = "quantization.check.latency.ms";
    public const string BatchSizeDistribution = "batch.size.distribution";

    // Counters (cumulative counts)
    public const string EmbeddingsGeneratedTotal = "embeddings.generated.total";
    public const string ModelsLoadedTotal = "models.loaded.total";
    public const string ErrorsTotal = "errors.total";
    public const string CacheHitsTotal = "cache.hits.total";
    public const string CacheMissesTotal = "cache.misses.total";

    // Gauges (instantaneous measurements)
    public const string ActiveRequests = "active.requests";
    public const string ModelCacheSizeMb = "model.cache.size.mb";
}
