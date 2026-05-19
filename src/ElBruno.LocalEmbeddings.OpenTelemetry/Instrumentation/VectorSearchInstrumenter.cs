using System.Diagnostics;
using ElBruno.LocalEmbeddings.OpenTelemetry.Internal;
using ElBruno.LocalEmbeddings.OpenTelemetry.Options;

namespace ElBruno.LocalEmbeddings.OpenTelemetry.Instrumentation;

/// <summary>
/// Helper for vector search trace instrumentation.
/// </summary>
internal sealed class VectorSearchInstrumenter
{
    private readonly LocalEmbeddingsOpenTelemetryOptions _options;
    private readonly IActivityBaggageProvider _baggageProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="VectorSearchInstrumenter"/> class.
    /// </summary>
    /// <param name="options">OpenTelemetry options.</param>
    /// <param name="baggageProvider">Optional baggage provider.</param>
    public VectorSearchInstrumenter(
        LocalEmbeddingsOpenTelemetryOptions options,
        IActivityBaggageProvider? baggageProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
        _baggageProvider = baggageProvider ?? new ActivityBaggageProvider();
    }

    /// <summary>
    /// Starts a vector search activity.
    /// </summary>
    /// <param name="corpusSize">Corpus size.</param>
    /// <param name="topK">Top K requested results.</param>
    /// <param name="similarityMetric">Similarity metric name.</param>
    /// <param name="embeddingDimension">Optional embedding dimension.</param>
    /// <returns>The started activity, or <see langword="null"/> if tracing is disabled.</returns>
    public Activity? StartSearchActivity(
        int corpusSize,
        int topK,
        string similarityMetric = "cosine",
        int? embeddingDimension = null)
    {
        if (!_options.EnableTracing)
        {
            return null;
        }

        var activity = OpenTelemetryActivitySource.Source.StartActivity(
            OpenTelemetryActivitySource.VectorSearch,
            ActivityKind.Internal);

        if (activity is null)
        {
            return null;
        }

        activity.SetTag(ActivityTags.LlmSystem, "local-embeddings");
        activity.SetTag(ActivityTags.CorpusSize, corpusSize);
        activity.SetTag(ActivityTags.TopK, topK);
        activity.SetTag(ActivityTags.SimilarityMetric, similarityMetric);
        activity.SetTag(ActivityTags.SamplingSampled, _options.ShouldSample());

        if (embeddingDimension.HasValue)
        {
            activity.SetTag(ActivityTags.EmbeddingDimension, embeddingDimension.Value);
        }

        BaggageExtensions.AttachBaggageToActivity(activity, _options, _baggageProvider);

        return activity;
    }

    /// <summary>
    /// Completes a vector search activity successfully.
    /// </summary>
    /// <param name="activity">The activity to complete.</param>
    /// <param name="resultsReturned">Number of results returned.</param>
    /// <param name="durationMs">Optional operation duration.</param>
    public static void CompleteSearch(Activity? activity, int resultsReturned, double? durationMs = null)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetTag(ActivityTags.ResultsReturned, resultsReturned);
        if (durationMs.HasValue)
        {
            activity.SetTag(ActivityTags.DurationMs, durationMs.Value);
        }

        activity.SetStatus(ActivityStatusCode.Ok);
    }

    /// <summary>
    /// Marks a vector search activity as failed.
    /// </summary>
    /// <param name="activity">The activity to mark as failed.</param>
    /// <param name="exception">Captured exception.</param>
    public static void RecordError(Activity? activity, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (activity is null)
        {
            return;
        }

        activity.SetTag(ActivityTags.ErrorType, exception.GetType().Name);
        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
    }
}
