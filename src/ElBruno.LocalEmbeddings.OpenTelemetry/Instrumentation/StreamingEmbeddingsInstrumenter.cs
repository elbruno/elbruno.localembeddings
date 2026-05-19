using System.Diagnostics;
using ElBruno.LocalEmbeddings.OpenTelemetry.Internal;
using ElBruno.LocalEmbeddings.OpenTelemetry.Options;

namespace ElBruno.LocalEmbeddings.OpenTelemetry.Instrumentation;

/// <summary>
/// Helper for streaming embedding trace instrumentation.
/// </summary>
internal sealed class StreamingEmbeddingsInstrumenter
{
    private readonly LocalEmbeddingsOpenTelemetryOptions _options;
    private readonly IActivityBaggageProvider _baggageProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamingEmbeddingsInstrumenter"/> class.
    /// </summary>
    /// <param name="options">OpenTelemetry options.</param>
    /// <param name="baggageProvider">Optional baggage provider.</param>
    public StreamingEmbeddingsInstrumenter(
        LocalEmbeddingsOpenTelemetryOptions options,
        IActivityBaggageProvider? baggageProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
        _baggageProvider = baggageProvider ?? new ActivityBaggageProvider();
    }

    /// <summary>
    /// Starts the root streaming activity.
    /// </summary>
    /// <param name="modelName">Optional model name.</param>
    /// <param name="bufferSize">Optional stream buffer size.</param>
    /// <param name="expectedBatchCount">Optional expected batch count.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>The started activity, or <see langword="null"/> if tracing is disabled.</returns>
    public Activity? StartStreamingActivity(
        string? modelName = null,
        int? bufferSize = null,
        int? expectedBatchCount = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableTracing)
        {
            return null;
        }

        var activity = OpenTelemetryActivitySource.Source.StartActivity(
            OpenTelemetryActivitySource.StreamingGenerate,
            ActivityKind.Internal);

        if (activity is null)
        {
            return null;
        }

        activity.SetTag(ActivityTags.LlmSystem, "local-embeddings");
        activity.SetTag(ActivityTags.LlmRequestType, "text");
        activity.SetTag(ActivityTags.SamplingSampled, _options.ShouldSample());

        if (!string.IsNullOrWhiteSpace(modelName))
        {
            activity.SetTag(ActivityTags.LlmRequestModel, modelName);
        }

        if (bufferSize.HasValue)
        {
            activity.SetTag(ActivityTags.BufferSize, bufferSize.Value);
        }

        if (expectedBatchCount.HasValue)
        {
            activity.SetTag(ActivityTags.BatchCount, expectedBatchCount.Value);
        }

        activity.SetTag("custom.cancellation_token_set", cancellationToken.CanBeCanceled);
        BaggageExtensions.AttachBaggageToActivity(activity, _options, _baggageProvider);

        return activity;
    }

    /// <summary>
    /// Starts a stream buffering activity.
    /// </summary>
    /// <param name="bufferedItemCount">Number of buffered items.</param>
    /// <param name="batchNumber">Batch number in stream sequence.</param>
    /// <returns>The started child activity, or <see langword="null"/>.</returns>
    public Activity? StartStreamBufferActivity(int bufferedItemCount, int batchNumber)
    {
        if (!_options.EnableTracing)
        {
            return null;
        }

        var activity = OpenTelemetryActivitySource.Source.StartActivity(
            OpenTelemetryActivitySource.StreamBuffer,
            ActivityKind.Internal);

        if (activity is null)
        {
            return null;
        }

        activity.SetTag(ActivityTags.BufferedItemCount, bufferedItemCount);
        activity.SetTag(ActivityTags.BatchNumber, batchNumber);

        return activity;
    }

    /// <summary>
    /// Starts a batch generation activity for one stream batch.
    /// </summary>
    /// <param name="batchNumber">Batch number in stream sequence.</param>
    /// <param name="batchSize">Batch size.</param>
    /// <returns>The started child activity, or <see langword="null"/>.</returns>
    public Activity? StartBatchGenerateActivity(int batchNumber, int batchSize)
    {
        if (!_options.EnableTracing)
        {
            return null;
        }

        var activity = OpenTelemetryActivitySource.Source.StartActivity(
            OpenTelemetryActivitySource.BatchGenerate,
            ActivityKind.Internal);

        if (activity is null)
        {
            return null;
        }

        activity.SetTag(ActivityTags.BatchNumber, batchNumber);
        activity.SetTag(ActivityTags.BatchSize, batchSize);

        return activity;
    }

    /// <summary>
    /// Records yielded item count and marks stream activity as successful.
    /// </summary>
    /// <param name="streamingActivity">Root streaming activity.</param>
    /// <param name="totalItemsYielded">Total yielded items.</param>
    /// <param name="batchesProcessed">Total processed batches.</param>
    public static void CompleteStreaming(Activity? streamingActivity, int totalItemsYielded, int batchesProcessed)
    {
        if (streamingActivity is null)
        {
            return;
        }

        streamingActivity.SetTag(ActivityTags.StreamItemCount, totalItemsYielded);
        streamingActivity.SetTag(ActivityTags.BatchCount, batchesProcessed);
        streamingActivity.AddEvent(new ActivityEvent(
            "streaming_completed",
            tags: new ActivityTagsCollection(new[]
            {
                new KeyValuePair<string, object?>(ActivityTags.StreamItemCount, totalItemsYielded),
                new KeyValuePair<string, object?>(ActivityTags.BatchCount, batchesProcessed),
            })));
        streamingActivity.SetStatus(ActivityStatusCode.Ok);
    }

    /// <summary>
    /// Marks stream activity as failed.
    /// </summary>
    /// <param name="activity">Root or child activity.</param>
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
