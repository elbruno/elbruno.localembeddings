using System.Diagnostics;

namespace ElBruno.LocalEmbeddings.Npu;

/// <summary>
/// Diagnostics source for NPU execution provider events.
/// </summary>
/// <remarks>
/// Provides OpenTelemetry-compatible diagnostics for tracking NPU execution provider
/// selection, fallback scenarios, and inference operations. Use this to monitor when
/// DirectML falls back to CPU or when NPU hardware is not detected.
/// </remarks>
public static class NpuDiagnostics
{
    /// <summary>
    /// Gets the diagnostic source name for NPU operations.
    /// </summary>
    public static readonly string SourceName = "ElBruno.LocalEmbeddings.Npu";

    private static readonly ActivitySource ActivitySource = new(SourceName, "1.0.0");

    /// <summary>
    /// Start an activity for NPU inference.
    /// </summary>
    /// <param name="executionProvider">The execution provider being used (e.g., "DirectML", "CPU").</param>
    /// <param name="modelPath">Optional path to the ONNX model file.</param>
    /// <returns>The started activity, or null if no listeners are registered.</returns>
    public static Activity? StartInference(string executionProvider, string? modelPath = null)
    {
        var activity = ActivitySource.StartActivity(
            "npu.inference",
            ActivityKind.Internal);

        if (activity is not null)
        {
            activity.SetTag("npu.execution_provider", executionProvider);
            if (modelPath is not null)
            {
                activity.SetTag("npu.model_path", modelPath);
            }
        }

        return activity;
    }

    /// <summary>
    /// Record a fallback event when NPU provider is unavailable.
    /// </summary>
    /// <param name="activity">The current activity, if any.</param>
    /// <param name="requestedProvider">The execution provider that was requested (e.g., "DirectML", "QNN").</param>
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
            activity.SetTag("npu.fallback", true);
            activity.SetTag("npu.requested_provider", requestedProvider);
            activity.SetTag("npu.actual_provider", actualProvider);
            activity.SetTag("npu.fallback_reason", reason);
            activity.AddEvent(new ActivityEvent(
                "npu.fallback",
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
        ActivitySource.CreateActivity("npu.fallback", ActivityKind.Internal, parentContext: default(ActivityContext), tags)?.Dispose();
    }

    /// <summary>
    /// Record device selection information.
    /// </summary>
    /// <param name="activity">The current activity, if any.</param>
    /// <param name="deviceId">The device ID selected.</param>
    /// <param name="deviceDescription">Description of the device.</param>
    /// <param name="isNpu">Whether the device is an NPU.</param>
    public static void RecordDeviceSelection(
        Activity? activity,
        int deviceId,
        string? deviceDescription,
        bool isNpu)
    {
        if (activity is not null)
        {
            activity.SetTag("npu.device_id", deviceId);
            if (deviceDescription is not null)
            {
                activity.SetTag("npu.device_description", deviceDescription);
            }
            activity.SetTag("npu.is_npu", isNpu);
        }
    }
}
