using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ElBruno.LocalEmbeddings;

/// <summary>
/// Represents a custom embedding generator that can be adapted to work with Microsoft.Extensions.AI.
/// </summary>
/// <remarks>
/// <para>
/// Implement this interface to create custom embedding backends (e.g., Ollama, OpenAI, Hugging Face APIs)
/// that integrate seamlessly with ElBruno.LocalEmbeddings middleware, caching, and DI patterns.
/// </para>
/// <para>
/// The interface provides metadata about the embedder (name, version, dimension size, capabilities)
/// and methods for generating embeddings from text.
/// </para>
/// <para>
/// <strong>Resource Management:</strong> If your implementation uses long-lived resources (HTTP clients,
/// connections), implement <see cref="System.IAsyncDisposable"/> to ensure proper cleanup.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> Implementations should be thread-safe and support concurrent calls
/// to <see cref="EmbedAsync"/> and <see cref="EmbedBatchAsync"/>.
/// </para>
/// </remarks>
/// <example>
/// Example implementation for an HTTP-based embedder:
/// <code>
/// public class OllamaEmbedder : ICustomEmbedder, IAsyncDisposable
/// {
///     private readonly HttpClient _httpClient;
///     
///     public string Name => "ollama-embeddings";
///     public string? Version => "1.0";
///     public int DimensionSize => 384;
///     public IReadOnlyList&lt;string&gt; Capabilities => new[] { "batching" };
///     
///     public async Task&lt;float[]&gt; EmbedAsync(string text, CancellationToken cancellationToken = default)
///     {
///         ArgumentNullException.ThrowIfNull(text);
///         // HTTP call to Ollama API
///         return await CallOllamaApiAsync(text, cancellationToken).ConfigureAwait(false);
///     }
///     
///     public async Task&lt;IEnumerable&lt;float[]&gt;&gt; EmbedBatchAsync(
///         IEnumerable&lt;string&gt; texts, 
///         CancellationToken cancellationToken = default)
///     {
///         ArgumentNullException.ThrowIfNull(texts);
///         // Batch HTTP call to Ollama API
///         return await CallOllamaBatchApiAsync(texts, cancellationToken).ConfigureAwait(false);
///     }
///     
///     public async ValueTask DisposeAsync()
///     {
///         _httpClient?.Dispose();
///     }
/// }
/// </code>
/// </example>
public interface ICustomEmbedder
{
    /// <summary>
    /// Gets the human-readable name of this embedder (e.g., "ollama-embeddings", "openai-ada-3").
    /// </summary>
    /// <remarks>
    /// This value is used in logging, debugging, and metadata reporting.
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// Gets an optional version string for the embedder or underlying model.
    /// </summary>
    /// <remarks>
    /// This can track model versions, API versions, or implementation versions.
    /// Returns <see langword="null"/> if version tracking is not applicable.
    /// </remarks>
    string? Version { get; }

    /// <summary>
    /// Gets the dimensionality of embeddings produced (e.g., 384, 768, 1536).
    /// </summary>
    /// <remarks>
    /// All embeddings returned by <see cref="EmbedAsync"/> and <see cref="EmbedBatchAsync"/>
    /// must have arrays of this length.
    /// </remarks>
    int DimensionSize { get; }

    /// <summary>
    /// Gets optional capability strings (e.g., "batching", "sparse", "streaming").
    /// </summary>
    /// <remarks>
    /// <para>
    /// Downstream code can use this to detect features and adapt behavior.
    /// Common capabilities include:
    /// </para>
    /// <list type="bullet">
    /// <item><term>batching</term><description>Supports efficient batch operations via <see cref="EmbedBatchAsync"/></description></item>
    /// <item><term>sparse</term><description>Produces sparse embeddings</description></item>
    /// <item><term>streaming</term><description>Supports streaming responses</description></item>
    /// <item><term>normalized</term><description>Embeddings are already L2-normalized</description></item>
    /// </list>
    /// <para>
    /// Returns an empty list if no special capabilities are advertised.
    /// </para>
    /// </remarks>
    IReadOnlyList<string> Capabilities { get; }

    /// <summary>
    /// Generates an embedding for a single text string.
    /// </summary>
    /// <param name="text">The text to embed. Must not be null.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A float array of size <see cref="DimensionSize"/> representing the embedding vector.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when cancellation is requested via <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// <para>
    /// Implementations should validate input (null checks, length limits) and propagate cancellation.
    /// </para>
    /// <para>
    /// For batch operations, prefer <see cref="EmbedBatchAsync"/> if your backend supports batching
    /// for better performance.
    /// </para>
    /// </remarks>
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates embeddings for multiple text strings in a batch.
    /// </summary>
    /// <param name="texts">The texts to embed. Must not be null.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// An enumerable of float arrays, each of size <see cref="DimensionSize"/>.
    /// The order of embeddings corresponds to the order of input texts.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="texts"/> is null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when cancellation is requested via <paramref name="cancellationToken"/>.</exception>
    /// <remarks>
    /// <para>
    /// Implementations can optimize batch processing (e.g., single HTTP request with multiple texts).
    /// If batch optimization is not available, the default behavior can iterate over <paramref name="texts"/>
    /// and call <see cref="EmbedAsync"/> for each item.
    /// </para>
    /// <para>
    /// <strong>Batch Size Limits:</strong> Consider imposing reasonable batch size limits to prevent
    /// API abuse or memory exhaustion. Document limits in your implementation.
    /// </para>
    /// </remarks>
    Task<IEnumerable<float[]>> EmbedBatchAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default);
}
