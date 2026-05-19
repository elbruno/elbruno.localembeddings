using System.Diagnostics;
using ElBruno.LocalEmbeddings.OpenTelemetry.Internal;
using ElBruno.LocalEmbeddings.OpenTelemetry.Instrumentation;
using ElBruno.LocalEmbeddings.OpenTelemetry.Options;
using Xunit;

namespace ElBruno.LocalEmbeddings.OpenTelemetry.Tests.Instrumentation;

public class StreamingEmbeddingsInstrumenterTests
{
    [Fact]
    public void StartStreamingActivity_CreatesActivityWithExpectedTags()
    {
        var options = new LocalEmbeddingsOpenTelemetryOptions { EnableTracing = true };
        var instrumenter = new StreamingEmbeddingsInstrumenter(options);
        using var listener = CreateListener();

        using var activity = instrumenter.StartStreamingActivity(
            modelName: "test-model",
            bufferSize: 16,
            expectedBatchCount: 2,
            cancellationToken: new CancellationToken(canceled: false));

        Assert.NotNull(activity);
        Assert.Equal(OpenTelemetryActivitySource.StreamingGenerate, activity.OperationName);
        Assert.Equal("test-model", activity.GetTagItem("llm.request.model"));
        Assert.Equal(16, activity.GetTagItem("custom.buffer_size"));
    }

    [Fact]
    public void CompleteStreaming_SetsCompletionTags()
    {
        using var listener = CreateListener();
        using var activity = OpenTelemetryActivitySource.Source.StartActivity("stream-test");

        StreamingEmbeddingsInstrumenter.CompleteStreaming(activity, totalItemsYielded: 10, batchesProcessed: 2);

        Assert.Equal(10, activity?.GetTagItem("custom.stream_item_count"));
        Assert.Equal(2, activity?.GetTagItem("custom.batch_count"));
        Assert.Equal(ActivityStatusCode.Ok, activity?.Status);
    }

    private static ActivityListener CreateListener()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "ElBruno.LocalEmbeddings",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
        };

        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}
