using System.Diagnostics;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalEmbeddings.Middleware;

/// <summary>
/// Middleware that adds OpenTelemetry tracing to embedding generation calls.
/// Records: model name, input count, duration, embedding dimensions.
/// </summary>
public sealed class OpenTelemetryEmbeddingMiddleware : DelegatingEmbeddingGenerator<string, Embedding<float>>
{
    private static readonly ActivitySource ActivitySource = new("ElBruno.LocalEmbeddings");
    private readonly string _modelName;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenTelemetryEmbeddingMiddleware"/> class.
    /// </summary>
    /// <param name="innerGenerator">The inner embedding generator to wrap.</param>
    /// <param name="modelName">The model name to record in telemetry. If null, uses metadata from inner generator.</param>
    public OpenTelemetryEmbeddingMiddleware(
        IEmbeddingGenerator<string, Embedding<float>> innerGenerator,
        string? modelName = null)
        : base(innerGenerator)
    {
        var metadata = innerGenerator.GetService<EmbeddingGeneratorMetadata>();
        _modelName = modelName ?? metadata?.DefaultModelId ?? "unknown";
    }

    /// <inheritdoc/>
    public override async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("GenerateEmbeddings", ActivityKind.Internal);
        
        var valuesList = values as IList<string> ?? values.ToList();
        var inputCount = valuesList.Count;

        activity?.SetTag("embedding.model", _modelName);
        activity?.SetTag("embedding.input_count", inputCount);

        var startTime = Stopwatch.GetTimestamp();
        
        try
        {
            var result = await base.GenerateAsync(valuesList, options, cancellationToken).ConfigureAwait(false);

            var elapsed = Stopwatch.GetElapsedTime(startTime);
            activity?.SetTag("embedding.duration_ms", elapsed.TotalMilliseconds);

            if (result.Count > 0)
            {
                activity?.SetTag("embedding.dimensions", result[0].Vector.Length);
            }

            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
