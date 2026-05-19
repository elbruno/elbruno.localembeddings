using System.Diagnostics;
using ElBruno.LocalEmbeddings.OpenTelemetry.Internal;
using ElBruno.LocalEmbeddings.OpenTelemetry.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ElBruno.LocalEmbeddings.OpenTelemetry.Instrumentation;

/// <summary>
/// Instrumented embedding generator that wraps the core generator with OpenTelemetry observability.
/// </summary>
/// <remarks>
/// This decorator implements the <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> interface
/// and adds distributed tracing, metrics collection, and structured events. All operations are
/// wrapped in OpenTelemetry Activities for observability.
/// </remarks>
public sealed class InstrumentedEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>, IDisposable, IAsyncDisposable
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _innerGenerator;
    private readonly IActivityBaggageProvider _baggageProvider;
    private readonly LocalEmbeddingsOpenTelemetryOptions _options;
    private readonly ILogger<InstrumentedEmbeddingGenerator>? _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstrumentedEmbeddingGenerator"/> class.
    /// </summary>
    /// <param name="innerGenerator">The underlying embedding generator to instrument.</param>
    /// <param name="options">Configuration options for OpenTelemetry instrumentation.</param>
    /// <param name="logger">Optional logger for diagnostic events.</param>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
    public InstrumentedEmbeddingGenerator(
        IEmbeddingGenerator<string, Embedding<float>> innerGenerator,
        LocalEmbeddingsOpenTelemetryOptions options,
        ILogger<InstrumentedEmbeddingGenerator>? logger = null)
        : this(innerGenerator, options, baggageProvider: null, logger)
    {
    }

    internal InstrumentedEmbeddingGenerator(
        IEmbeddingGenerator<string, Embedding<float>> innerGenerator,
        LocalEmbeddingsOpenTelemetryOptions options,
        IActivityBaggageProvider? baggageProvider,
        ILogger<InstrumentedEmbeddingGenerator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(innerGenerator);
        ArgumentNullException.ThrowIfNull(options);

        options.Validate();

        _innerGenerator = innerGenerator;
        _baggageProvider = baggageProvider ?? new ActivityBaggageProvider();
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    public EmbeddingGeneratorMetadata Metadata =>
        _innerGenerator.GetService<EmbeddingGeneratorMetadata>() ??
        new EmbeddingGeneratorMetadata("InstrumentedLocalEmbeddings");

    /// <inheritdoc/>
    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ObjectDisposedException.ThrowIf(_disposed, GetType());

        var valuesList = values as IList<string> ?? values.ToList();

        if (!_options.EnableTracing)
        {
            return await _innerGenerator.GenerateAsync(valuesList, options, cancellationToken).ConfigureAwait(false);
        }

        bool shouldSample = _options.ShouldSample();
        var startTime = Environment.TickCount64;
        
        using var activity = OpenTelemetryActivitySource.Source.StartActivity(
            OpenTelemetryActivitySource.GenerateEmbeddings,
            ActivityKind.Internal);

        if (activity is not null)
        {
            activity.SetTag(ActivityTags.LlmSystem, "local-embeddings");
            activity.SetTag(ActivityTags.LlmRequestType, "text");
            activity.SetTag(ActivityTags.InputCount, valuesList.Count);

            var metadata = Metadata;
            if (metadata is not null)
            {
                activity.SetTag(ActivityTags.LlmRequestModel, metadata.DefaultModelId ?? "unknown");
            }

            activity.SetTag(ActivityTags.SamplingSampled, shouldSample);
            BaggageExtensions.AttachBaggageToActivity(activity, _options, _baggageProvider);
        }

        try
        {
            _logger?.LogDebug("Starting embedding generation for {Count} items", valuesList.Count);
            var result = await _innerGenerator.GenerateAsync(valuesList, options, cancellationToken).ConfigureAwait(false);

            if (activity is not null)
            {
                var durationMs = Environment.TickCount64 - startTime;
                activity.SetTag(ActivityTags.DurationMs, durationMs);
                activity.SetTag(ActivityTags.OutputCount, result.Count);
                
                if (result.Count > 0)
                {
                    activity.SetTag(ActivityTags.DimensionCount, result[0].Vector.Length);
                }

                activity.SetStatus(ActivityStatusCode.Ok);
            }

            if (shouldSample && _options.EnableMetrics && _options.MetricMeter is not null)
            {
                _options.MetricMeter.RecordEmbeddingLatency(Environment.TickCount64 - startTime);
                _options.MetricMeter.RecordBatchSize(valuesList.Count);
                _options.MetricMeter.IncrementEmbeddingsGenerated(valuesList.Count);
            }

            _logger?.LogDebug("Successfully generated {Count} embeddings in {Duration}ms", valuesList.Count, Environment.TickCount64 - startTime);
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to generate embeddings for {Count} items", valuesList.Count);

            if (activity is not null)
            {
                var durationMs = Environment.TickCount64 - startTime;
                activity.SetTag(ActivityTags.DurationMs, durationMs);
                activity.SetTag(ActivityTags.ErrorType, ex.GetType().Name);

                if (_options.RecordExceptionDetails)
                {
                    activity.SetTag(ActivityTags.ExceptionMessage, ex.Message);
                    if (ex.StackTrace is not null)
                    {
                        activity.SetTag(ActivityTags.ExceptionStacktrace, ex.StackTrace);
                    }
                }

                activity.SetStatus(ActivityStatusCode.Error, ex.Message);
            }

            if (shouldSample && _options.EnableMetrics && _options.MetricMeter is not null)
            {
                _options.MetricMeter.IncrementErrors();
            }

            throw;
        }
    }

    /// <inheritdoc/>
    public object? GetService(Type serviceType, object? serviceKey = null)
        => _innerGenerator.GetService(serviceType, serviceKey);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_innerGenerator is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_innerGenerator is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
    }
}
