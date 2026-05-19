using System.Diagnostics;
using ElBruno.LocalEmbeddings.OpenTelemetry.Internal;
using Xunit;

namespace ElBruno.LocalEmbeddings.OpenTelemetry.Tests.Internal;

/// <summary>
/// Unit tests for OpenTelemetry activity source names and operation helpers.
/// </summary>
public class OpenTelemetryActivitySourceTests
{
    [Fact]
    public void ActivityNames_ContainStreamingAndVectorSearchOperations()
    {
        Assert.Equal("ElBruno.LocalEmbeddings.StreamingGenerate", OpenTelemetryActivitySource.StreamingGenerate);
        Assert.Equal("ElBruno.LocalEmbeddings.StreamBuffer", OpenTelemetryActivitySource.StreamBuffer);
        Assert.Equal("ElBruno.LocalEmbeddings.StreamYield", OpenTelemetryActivitySource.StreamYield);
        Assert.Equal("ElBruno.LocalEmbeddings.VectorSearch", OpenTelemetryActivitySource.VectorSearch);
    }

    [Theory]
    [InlineData(OpenTelemetryActivitySource.StreamingGenerate)]
    [InlineData(OpenTelemetryActivitySource.StreamBuffer)]
    [InlineData(OpenTelemetryActivitySource.StreamYield)]
    [InlineData(OpenTelemetryActivitySource.VectorSearch)]
    public void Source_StartActivity_CreatesExpectedOperation(string activityName)
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "ElBruno.LocalEmbeddings",
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };

        ActivitySource.AddActivityListener(listener);

        using var activity = OpenTelemetryActivitySource.Source.StartActivity(activityName, ActivityKind.Internal);

        Assert.NotNull(activity);
        Assert.Equal(activityName, activity.OperationName);
        Assert.Equal("ElBruno.LocalEmbeddings", activity.Source.Name);
    }

    [Fact]
    public void ActivityTags_DefineStreamingAndVectorSearchAttributes()
    {
        Assert.Equal("custom.stream_item_count", ActivityTags.StreamItemCount);
        Assert.Equal("custom.buffer_size", ActivityTags.BufferSize);
        Assert.Equal("custom.batch_count", ActivityTags.BatchCount);
        Assert.Equal("custom.batch_number", ActivityTags.BatchNumber);
        Assert.Equal("custom.buffered_item_count", ActivityTags.BufferedItemCount);
        Assert.Equal("custom.corpus_size", ActivityTags.CorpusSize);
        Assert.Equal("custom.top_k", ActivityTags.TopK);
        Assert.Equal("custom.similarity_metric", ActivityTags.SimilarityMetric);
        Assert.Equal("custom.results_returned", ActivityTags.ResultsReturned);
        Assert.Equal("custom.embedding_dimension", ActivityTags.EmbeddingDimension);
        Assert.Equal("baggage.", ActivityTags.BaggagePrefix);
    }
}
