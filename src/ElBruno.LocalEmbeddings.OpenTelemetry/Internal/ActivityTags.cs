namespace ElBruno.LocalEmbeddings.OpenTelemetry.Internal;

/// <summary>
/// Span attribute name constants for OpenTelemetry instrumentation.
/// </summary>
internal static class ActivityTags
{
    // LLM semantic attributes (OpenTelemetry standard)
    public const string LlmSystem = "llm.system";
    public const string LlmRequestModel = "llm.request.model";
    public const string LlmRequestType = "llm.request.type";
    public const string LlmUsageInputTokens = "llm.usage.input_tokens";
    public const string LlmUsageOutputTokens = "llm.usage.output_tokens";

    // Custom attributes
    public const string ModelName = "model.name";
    public const string ModelVariant = "model.variant";
    public const string QuantizationFormat = "quantization.format";
    public const string InputCount = "input.count";
    public const string OutputCount = "output.count";
    public const string BatchSize = "batch.size";
    public const string DimensionCount = "dimension.count";
    public const string CacheHit = "cache.hit";
    public const string ErrorType = "error.type";
    public const string DurationMs = "duration.ms";
    public const string SamplingSampled = "sampling.sampled";
    public const string StreamItemCount = "custom.stream_item_count";
    public const string BufferSize = "custom.buffer_size";
    public const string BatchCount = "custom.batch_count";
    public const string BatchNumber = "custom.batch_number";
    public const string BufferedItemCount = "custom.buffered_item_count";
    public const string CorpusSize = "custom.corpus_size";
    public const string TopK = "custom.top_k";
    public const string SimilarityMetric = "custom.similarity_metric";
    public const string ResultsReturned = "custom.results_returned";
    public const string EmbeddingDimension = "custom.embedding_dimension";
    public const string BaggagePrefix = "baggage.";

    // Standard OpenTelemetry attributes
    public const string HttpStatusCode = "http.status_code";
    public const string ExceptionType = "exception.type";
    public const string ExceptionMessage = "exception.message";
    public const string ExceptionStacktrace = "exception.stacktrace";
}
