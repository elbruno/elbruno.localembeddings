using ElBruno.LocalEmbeddings.Options;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalEmbeddings;

/// <summary>
/// Factory methods for creating custom embedder adapters.
/// </summary>
/// <remarks>
/// Use this class to adapt an <see cref="ICustomEmbedder"/> implementation to the
/// Microsoft.Extensions.AI <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> interface.
/// </remarks>
public static class CustomEmbedder
{
    /// <summary>
    /// Creates an <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> from a custom embedder implementation.
    /// </summary>
    /// <param name="embedder">The custom embedder to adapt. Must not be null.</param>
    /// <param name="modelName">
    /// Human-readable model identifier (defaults to the embedder's <see cref="ICustomEmbedder.Name"/> if null).
    /// </param>
    /// <param name="options">
    /// Configuration options for the adapter (e.g., normalization settings).
    /// If null, defaults to <see cref="CustomEmbedderOptions"/> with default values.
    /// </param>
    /// <returns>
    /// An <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> that delegates to the custom embedder
    /// and produces <see cref="Embedding{T}"/> results compatible with Microsoft.Extensions.AI.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="embedder"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// The adapter wraps the custom embedder and provides:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Conversion from raw <c>float[]</c> embeddings to <see cref="Embedding{T}"/></description></item>
    /// <item><description>Optional L2 normalization based on <paramref name="options"/></description></item>
    /// <item><description>Metadata reporting via <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/></description></item>
    /// <item><description>Resource cleanup if the embedder implements <see cref="IAsyncDisposable"/></description></item>
    /// </list>
    /// <para>
    /// The adapter is compatible with ElBruno.LocalEmbeddings middleware (caching, retries, telemetry).
    /// </para>
    /// </remarks>
    /// <example>
    /// Basic usage:
    /// <code>
    /// var ollamaEmbedder = new OllamaEmbedder("http://localhost:11434");
    /// var generator = CustomEmbedder.CreateAdapter(ollamaEmbedder);
    /// 
    /// var embeddings = await generator.GenerateAsync(new[] { "Hello world" });
    /// </code>
    /// 
    /// With custom options:
    /// <code>
    /// var options = new CustomEmbedderOptions
    /// {
    ///     NormalizeEmbeddings = false // Ollama already normalizes
    /// };
    /// var generator = CustomEmbedder.CreateAdapter(ollamaEmbedder, "ollama-nomic-embed-text", options);
    /// </code>
    /// </example>
    public static IEmbeddingGenerator<string, Embedding<float>> CreateAdapter(
        ICustomEmbedder embedder,
        string? modelName = null,
        CustomEmbedderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(embedder);

        return new CustomEmbedderAdapter(
            embedder,
            modelName ?? embedder.Name,
            options ?? new CustomEmbedderOptions());
    }
}
