using Microsoft.Extensions.AI;

namespace ElBruno.LocalEmbeddings.Middleware;

/// <summary>
/// Extension methods for adding middleware to embedding generators.
/// </summary>
public static class EmbeddingMiddlewareExtensions
{
    /// <summary>
    /// Adds OpenTelemetry tracing middleware to the embedding generator pipeline.
    /// </summary>
    /// <param name="generator">The embedding generator to wrap.</param>
    /// <param name="modelName">Optional model name to record in telemetry. If null, uses generator metadata.</param>
    /// <returns>A wrapped embedding generator with OpenTelemetry tracing.</returns>
    /// <remarks>
    /// Records the following tags on each embedding generation:
    /// <list type="bullet">
    /// <item><description><c>embedding.model</c> — Model name</description></item>
    /// <item><description><c>embedding.input_count</c> — Number of input texts</description></item>
    /// <item><description><c>embedding.duration_ms</c> — Generation duration in milliseconds</description></item>
    /// <item><description><c>embedding.dimensions</c> — Embedding vector dimensions</description></item>
    /// </list>
    /// <para>
    /// Activities are emitted to the <c>"ElBruno.LocalEmbeddings"</c> activity source.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var generator = new LocalEmbeddingGenerator()
    ///     .UseOpenTelemetry();
    /// </code>
    /// </example>
    public static IEmbeddingGenerator<string, Embedding<float>> UseOpenTelemetry(
        this IEmbeddingGenerator<string, Embedding<float>> generator,
        string? modelName = null)
    {
        ArgumentNullException.ThrowIfNull(generator);
        return new OpenTelemetryEmbeddingMiddleware(generator, modelName);
    }

    /// <summary>
    /// Adds retry middleware with exponential backoff to the embedding generator pipeline.
    /// </summary>
    /// <param name="generator">The embedding generator to wrap.</param>
    /// <param name="maxRetries">Maximum number of retry attempts. Default is 3.</param>
    /// <param name="baseDelay">Base delay between retries. Default is 200ms. Uses exponential backoff (delay * 2^attempt).</param>
    /// <returns>A wrapped embedding generator with retry logic.</returns>
    /// <remarks>
    /// Only retries on transient failures:
    /// <list type="bullet">
    /// <item><description><see cref="Microsoft.ML.OnnxRuntime.OnnxRuntimeException"/></description></item>
    /// <item><description><see cref="IOException"/></description></item>
    /// </list>
    /// <para>
    /// Backoff formula: <c>baseDelay * 2^(attempt - 1)</c>.
    /// For example, with baseDelay=200ms: 200ms, 400ms, 800ms, ...
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var generator = new LocalEmbeddingGenerator()
    ///     .UseRetry(maxRetries: 5);
    /// </code>
    /// </example>
    public static IEmbeddingGenerator<string, Embedding<float>> UseRetry(
        this IEmbeddingGenerator<string, Embedding<float>> generator,
        int maxRetries = 3,
        TimeSpan? baseDelay = null)
    {
        ArgumentNullException.ThrowIfNull(generator);
        return new RetryEmbeddingMiddleware(generator, maxRetries, baseDelay);
    }
}
