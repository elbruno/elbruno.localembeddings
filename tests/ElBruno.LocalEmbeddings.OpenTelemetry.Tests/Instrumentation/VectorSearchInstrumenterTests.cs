using System.Diagnostics;
using ElBruno.LocalEmbeddings.OpenTelemetry.Internal;
using ElBruno.LocalEmbeddings.OpenTelemetry.Instrumentation;
using ElBruno.LocalEmbeddings.OpenTelemetry.Options;
using Xunit;

namespace ElBruno.LocalEmbeddings.OpenTelemetry.Tests.Instrumentation;

public class VectorSearchInstrumenterTests
{
    [Fact]
    public void StartSearchActivity_CreatesActivityWithExpectedTags()
    {
        var options = new LocalEmbeddingsOpenTelemetryOptions { EnableTracing = true };
        var instrumenter = new VectorSearchInstrumenter(options);
        using var listener = CreateListener();

        using var activity = instrumenter.StartSearchActivity(corpusSize: 1000, topK: 5, similarityMetric: "cosine", embeddingDimension: 384);

        Assert.NotNull(activity);
        Assert.Equal(OpenTelemetryActivitySource.VectorSearch, activity.OperationName);
        Assert.Equal(1000, activity.GetTagItem("custom.corpus_size"));
        Assert.Equal(5, activity.GetTagItem("custom.top_k"));
        Assert.Equal("cosine", activity.GetTagItem("custom.similarity_metric"));
    }

    [Fact]
    public void CompleteSearch_SetsSuccessTagsAndStatus()
    {
        using var listener = CreateListener();
        using var activity = OpenTelemetryActivitySource.Source.StartActivity("vector-search");

        VectorSearchInstrumenter.CompleteSearch(activity, resultsReturned: 4, durationMs: 1.25);

        Assert.Equal(4, activity?.GetTagItem("custom.results_returned"));
        Assert.Equal(1.25, activity?.GetTagItem("duration.ms"));
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
