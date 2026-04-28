using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ElBruno.LocalEmbeddings.Options;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalEmbeddings;

/// <summary>
/// Internal adapter that converts <see cref="ICustomEmbedder"/> to <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/>.
/// </summary>
internal sealed class CustomEmbedderAdapter : IEmbeddingGenerator<string, Embedding<float>>, IAsyncDisposable, IDisposable
{
    private readonly ICustomEmbedder _embedder;
    private readonly CustomEmbedderOptions _options;
    private readonly EmbeddingGeneratorMetadata _metadata;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomEmbedderAdapter"/> class.
    /// </summary>
    /// <param name="embedder">The custom embedder to adapt.</param>
    /// <param name="modelName">The model identifier for metadata.</param>
    /// <param name="options">Configuration options.</param>
    public CustomEmbedderAdapter(
        ICustomEmbedder embedder,
        string modelName,
        CustomEmbedderOptions options)
    {
        _embedder = embedder ?? throw new ArgumentNullException(nameof(embedder));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        _metadata = new EmbeddingGeneratorMetadata(
            providerName: embedder.Name,
            providerUri: null,
            defaultModelId: modelName,
            defaultModelDimensions: embedder.DimensionSize);
    }

    /// <inheritdoc />
    public EmbeddingGeneratorMetadata Metadata => _metadata;

    /// <inheritdoc />
    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);

        // Convert IEnumerable to list to support multiple enumeration
        var valuesList = values.ToList();
        if (valuesList.Count == 0)
        {
            return new GeneratedEmbeddings<Embedding<float>>(Array.Empty<Embedding<float>>());
        }

        // Call the custom embedder's batch method
        var rawEmbeddings = await _embedder.EmbedBatchAsync(valuesList, cancellationToken).ConfigureAwait(false);

        // Convert raw float[] to Embedding<float> with optional normalization
        var embeddings = new List<Embedding<float>>();
        foreach (var rawEmbedding in rawEmbeddings)
        {
            var embedding = ProcessEmbedding(rawEmbedding);
            embeddings.Add(embedding);
        }

        return new GeneratedEmbeddings<Embedding<float>>(embeddings)
        {
            Usage = new UsageDetails
            {
                InputTokenCount = valuesList.Sum(v => EstimateTokenCount(v)),
                TotalTokenCount = valuesList.Sum(v => EstimateTokenCount(v))
            }
        };
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return serviceKey is null && serviceType.IsInstanceOfType(_embedder) ? _embedder : null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_embedder is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _disposed = true;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (_embedder is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else if (_embedder is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Processes a raw embedding (applies normalization if configured).
    /// </summary>
    private Embedding<float> ProcessEmbedding(float[] rawEmbedding)
    {
        if (_options.NormalizeEmbeddings)
        {
            return new Embedding<float>(Normalize(rawEmbedding));
        }

        return new Embedding<float>(rawEmbedding);
    }

    /// <summary>
    /// Applies L2 normalization to an embedding vector.
    /// </summary>
    private static float[] Normalize(float[] vector)
    {
        var magnitude = 0f;
        for (var i = 0; i < vector.Length; i++)
        {
            magnitude += vector[i] * vector[i];
        }

        magnitude = MathF.Sqrt(magnitude);

        if (magnitude < 1e-12f) // Avoid division by zero
        {
            return vector;
        }

        var normalized = new float[vector.Length];
        for (var i = 0; i < vector.Length; i++)
        {
            normalized[i] = vector[i] / magnitude;
        }

        return normalized;
    }

    /// <summary>
    /// Estimates token count for usage reporting (simple whitespace-based heuristic).
    /// </summary>
    private static int EstimateTokenCount(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        // Simple heuristic: split on whitespace
        return text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
