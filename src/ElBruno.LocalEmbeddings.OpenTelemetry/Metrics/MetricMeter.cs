using System.Diagnostics.Metrics;
using ElBruno.LocalEmbeddings.OpenTelemetry.Internal;

namespace ElBruno.LocalEmbeddings.OpenTelemetry.Metrics;

/// <summary>
/// Manages OpenTelemetry metric collection and recording for the embedding generation pipeline.
/// </summary>
/// <remarks>
/// This class encapsulates all 11 metrics defined for the ElBruno.LocalEmbeddings instrumentation:
/// - 4 Histograms: embedding_latency_ms, model_load_ms, quantization_check_ms, batch_size_distribution
/// - 5 Counters: embeddings_generated_total, models_loaded_total, errors_total, cache_hits_total, cache_misses_total
/// - 2 Gauges: active_requests, model_cache_size_mb
/// </remarks>
public sealed class MetricMeter : IDisposable
{
    private readonly Meter _meter;
    
    // Histograms (latency measurements in milliseconds)
    private readonly Histogram<double> _embeddingLatencyMs;
    private readonly Histogram<double> _modelLoadLatencyMs;
    private readonly Histogram<double> _quantizationCheckLatencyMs;
    private readonly Histogram<int> _batchSizeDistribution;
    
    // Counters (cumulative counts)
    private readonly Counter<long> _embeddingsGeneratedTotal;
    private readonly Counter<long> _modelsLoadedTotal;
    private readonly Counter<long> _errorsTotal;
    private readonly Counter<long> _cacheHitsTotal;
    private readonly Counter<long> _cacheMissesTotal;
    
    // Gauges (instantaneous measurements) - stored for later observable registration
    private long _activeRequests;
    private long _modelCacheSizeMb;
    
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetricMeter"/> class.
    /// </summary>
    public MetricMeter()
    {
        _meter = new Meter("ElBruno.LocalEmbeddings", "1.0.0");
        
        // Initialize histograms
        _embeddingLatencyMs = _meter.CreateHistogram<double>(
            MetricNames.EmbeddingLatencyMs,
            "ms",
            "Latency of embedding generation in milliseconds");
        
        _modelLoadLatencyMs = _meter.CreateHistogram<double>(
            MetricNames.ModelLoadLatencyMs,
            "ms",
            "Time taken to load the model in milliseconds");
        
        _quantizationCheckLatencyMs = _meter.CreateHistogram<double>(
            MetricNames.QuantizationCheckLatencyMs,
            "ms",
            "Time taken to check and apply quantization in milliseconds");
        
        _batchSizeDistribution = _meter.CreateHistogram<int>(
            MetricNames.BatchSizeDistribution,
            "items",
            "Distribution of batch sizes for embedding generation");
        
        // Initialize counters
        _embeddingsGeneratedTotal = _meter.CreateCounter<long>(
            MetricNames.EmbeddingsGeneratedTotal,
            "embeddings",
            "Total number of embeddings generated");
        
        _modelsLoadedTotal = _meter.CreateCounter<long>(
            MetricNames.ModelsLoadedTotal,
            null,
            "Total number of models loaded");
        
        _errorsTotal = _meter.CreateCounter<long>(
            MetricNames.ErrorsTotal,
            null,
            "Total number of errors encountered");
        
        _cacheHitsTotal = _meter.CreateCounter<long>(
            MetricNames.CacheHitsTotal,
            "hits",
            "Total number of cache hits");
        
        _cacheMissesTotal = _meter.CreateCounter<long>(
            MetricNames.CacheMissesTotal,
            "misses",
            "Total number of cache misses");
        
        // Initialize gauge state (observables are created elsewhere)
        _activeRequests = 0;
        _modelCacheSizeMb = 0;
    }

    /// <summary>
    /// Records embedding generation latency in milliseconds.
    /// </summary>
    /// <param name="durationMs">The duration in milliseconds.</param>
    /// <param name="tags">Optional measurement tags.</param>
    public void RecordEmbeddingLatency(double durationMs, KeyValuePair<string, object?>[]? tags = null)
    {
        ThrowIfDisposed();
        if (tags is not null)
        {
            _embeddingLatencyMs.Record(durationMs, tags);
        }
        else
        {
            _embeddingLatencyMs.Record(durationMs);
        }
    }

    /// <summary>
    /// Records model load latency in milliseconds.
    /// </summary>
    /// <param name="durationMs">The duration in milliseconds.</param>
    /// <param name="tags">Optional measurement tags.</param>
    public void RecordModelLoadLatency(double durationMs, KeyValuePair<string, object?>[]? tags = null)
    {
        ThrowIfDisposed();
        if (tags is not null)
        {
            _modelLoadLatencyMs.Record(durationMs, tags);
        }
        else
        {
            _modelLoadLatencyMs.Record(durationMs);
        }
    }

    /// <summary>
    /// Records quantization check latency in milliseconds.
    /// </summary>
    /// <param name="durationMs">The duration in milliseconds.</param>
    /// <param name="tags">Optional measurement tags.</param>
    public void RecordQuantizationCheckLatency(double durationMs, KeyValuePair<string, object?>[]? tags = null)
    {
        ThrowIfDisposed();
        if (tags is not null)
        {
            _quantizationCheckLatencyMs.Record(durationMs, tags);
        }
        else
        {
            _quantizationCheckLatencyMs.Record(durationMs);
        }
    }

    /// <summary>
    /// Records batch size distribution.
    /// </summary>
    /// <param name="batchSize">The size of the batch.</param>
    /// <param name="tags">Optional measurement tags.</param>
    public void RecordBatchSize(int batchSize, KeyValuePair<string, object?>[]? tags = null)
    {
        ThrowIfDisposed();
        if (tags is not null)
        {
            _batchSizeDistribution.Record(batchSize, tags);
        }
        else
        {
            _batchSizeDistribution.Record(batchSize);
        }
    }

    /// <summary>
    /// Increments the total count of embeddings generated.
    /// </summary>
    /// <param name="count">The number of embeddings to add (default: 1).</param>
    /// <param name="tags">Optional measurement tags.</param>
    public void IncrementEmbeddingsGenerated(long count = 1, KeyValuePair<string, object?>[]? tags = null)
    {
        ThrowIfDisposed();
        if (tags is not null)
        {
            _embeddingsGeneratedTotal.Add(count, tags);
        }
        else
        {
            _embeddingsGeneratedTotal.Add(count);
        }
    }

    /// <summary>
    /// Increments the total count of models loaded.
    /// </summary>
    /// <param name="count">The number of models to add (default: 1).</param>
    /// <param name="tags">Optional measurement tags.</param>
    public void IncrementModelsLoaded(long count = 1, KeyValuePair<string, object?>[]? tags = null)
    {
        ThrowIfDisposed();
        if (tags is not null)
        {
            _modelsLoadedTotal.Add(count, tags);
        }
        else
        {
            _modelsLoadedTotal.Add(count);
        }
    }

    /// <summary>
    /// Increments the total count of errors.
    /// </summary>
    /// <param name="count">The number of errors to add (default: 1).</param>
    /// <param name="tags">Optional measurement tags.</param>
    public void IncrementErrors(long count = 1, KeyValuePair<string, object?>[]? tags = null)
    {
        ThrowIfDisposed();
        if (tags is not null)
        {
            _errorsTotal.Add(count, tags);
        }
        else
        {
            _errorsTotal.Add(count);
        }
    }

    /// <summary>
    /// Increments the total count of cache hits.
    /// </summary>
    /// <param name="count">The number of cache hits to add (default: 1).</param>
    /// <param name="tags">Optional measurement tags.</param>
    public void IncrementCacheHits(long count = 1, KeyValuePair<string, object?>[]? tags = null)
    {
        ThrowIfDisposed();
        if (tags is not null)
        {
            _cacheHitsTotal.Add(count, tags);
        }
        else
        {
            _cacheHitsTotal.Add(count);
        }
    }

    /// <summary>
    /// Increments the total count of cache misses.
    /// </summary>
    /// <param name="count">The number of cache misses to add (default: 1).</param>
    /// <param name="tags">Optional measurement tags.</param>
    public void IncrementCacheMisses(long count = 1, KeyValuePair<string, object?>[]? tags = null)
    {
        ThrowIfDisposed();
        if (tags is not null)
        {
            _cacheMissesTotal.Add(count, tags);
        }
        else
        {
            _cacheMissesTotal.Add(count);
        }
    }

    /// <summary>
    /// Sets the current number of active requests.
    /// </summary>
    /// <param name="count">The number of active requests.</param>
    public void SetActiveRequests(long count)
    {
        ThrowIfDisposed();
        Interlocked.Exchange(ref _activeRequests, count);
    }

    /// <summary>
    /// Gets the current number of active requests.
    /// </summary>
    public long GetActiveRequests()
    {
        ThrowIfDisposed();
        return Interlocked.Read(ref _activeRequests);
    }

    /// <summary>
    /// Sets the current model cache size in megabytes.
    /// </summary>
    /// <param name="sizeMb">The cache size in megabytes.</param>
    public void SetModelCacheSizeMb(long sizeMb)
    {
        ThrowIfDisposed();
        Interlocked.Exchange(ref _modelCacheSizeMb, sizeMb);
    }

    /// <summary>
    /// Gets the current model cache size in megabytes.
    /// </summary>
    public long GetModelCacheSizeMb()
    {
        ThrowIfDisposed();
        return Interlocked.Read(ref _modelCacheSizeMb);
    }

    /// <summary>
    /// Gets the underlying Meter instance for custom observable registration.
    /// </summary>
    /// <remarks>
    /// Use this to create observable gauges or other custom metric types.
    /// </remarks>
    public Meter GetMeter()
    {
        ThrowIfDisposed();
        return _meter;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _meter.Dispose();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, GetType());
    }
}
