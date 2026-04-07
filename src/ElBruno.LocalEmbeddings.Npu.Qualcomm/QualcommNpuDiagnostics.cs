using System.Diagnostics;

namespace ElBruno.LocalEmbeddings.Npu.Qualcomm;

/// <summary>
/// Diagnostics source for Qualcomm QNN execution provider events.
/// </summary>
/// <remarks>
/// Provides OpenTelemetry-compatible diagnostics for tracking Qualcomm QNN execution provider
/// selection, fallback scenarios, and inference operations. Use this to monitor when
/// QNN falls back to CPU or when NPU hardware is not available.
/// </remarks>
public static class QualcommNpuDiagnostics
{
    /// <summary>
    /// Gets the diagnostic source name for Qualcomm NPU operations.
    /// </summary>
    public static readonly string SourceName = "ElBruno.LocalEmbeddings.Npu.Qualcomm";

    private static readonly ActivitySource ActivitySource = new(SourceName, "1.0.0");

    /// <summary>
    /// Start an activity for Qualcomm NPU inference.
    /// </summary>
    /// <param name="executionProvider">The execution provider being used (e.g., "QNN", "CPU").</param>
    /// <param name="modelPath">Optional path to the ONNX model file.</param>
    /// <returns>The started activity, or null if no listeners are registered.</returns>
    public static Activity? StartInference(string executionProvider, string? modelPath = null)
    {
        var activity = ActivitySource.StartActivity(
            "qnn.inference",
            ActivityKind.Internal);

        if (activity is not null)
        {
            activity.SetTag("qnn.execution_provider", executionProvider);
            if (modelPath is not null)
            {
                activity.SetTag("qnn.model_path", modelPath);
            }
        }

        return activity;
    }

    /// <summary>
    /// Record a fallback event when QNN provider is unavailable.
    /// </summary>
    /// <param name="activity">The current activity, if any.</param>
    /// <param name="requestedProvider">The execution provider that was requested (e.g., "QNN").</param>
    /// <param name="actualProvider">The execution provider that will be used instead (e.g., "CPU").</param>
    /// <param name="reason">The reason for the fallback.</param>
    public static void RecordFallback(
        Activity? activity,
        string requestedProvider,
        string actualProvider,
        string reason)
    {
        if (activity is not null)
        {
            activity.SetTag("qnn.fallback", true);
            activity.SetTag("qnn.requested_provider", requestedProvider);
            activity.SetTag("qnn.actual_provider", actualProvider);
            activity.SetTag("qnn.fallback_reason", reason);
            activity.AddEvent(new ActivityEvent(
                "qnn.fallback",
                tags: new ActivityTagsCollection
                {
                    { "requested_provider", requestedProvider },
                    { "actual_provider", actualProvider },
                    { "reason", reason }
                }));
        }

        // Also emit a diagnostic event for listeners without tracing
        var tags = new ActivityTagsCollection
        {
            { "requested_provider", requestedProvider },
            { "actual_provider", actualProvider },
            { "reason", reason }
        };
        ActivitySource.CreateActivity("qnn.fallback", ActivityKind.Internal, parentContext: default(ActivityContext), tags)?.Dispose();
    }
}
