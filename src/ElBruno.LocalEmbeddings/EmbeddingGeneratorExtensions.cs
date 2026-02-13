using Microsoft.Extensions.AI;

namespace ElBruno.LocalEmbeddings;

/// <summary>
/// Convenience extension methods for <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/>
/// that simplify single-string embedding generation.
/// </summary>
/// <remarks>
/// These methods eliminate the need to wrap a single string in a collection and index
/// the result, making the most common use case — embedding one text — as simple as possible.
/// </remarks>
public static class EmbeddingGeneratorExtensions
{
    /// <summary>
    /// Generates embeddings for a single string value.
    /// </summary>
    /// <param name="generator">The embedding generator.</param>
    /// <param name="value">The text to generate an embedding for.</param>
    /// <param name="options">Optional embedding generation options.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="GeneratedEmbeddings{TEmbedding}"/> containing a single embedding.
    /// Access the embedding via <c>result[0]</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="generator"/> or <paramref name="value"/> is null.
    /// </exception>
    /// <example>
    /// <code>
    /// var result = await generator.GenerateAsync("Hello, world!");
    /// float[] vector = result[0].Vector.ToArray();
    /// Console.WriteLine($"Dimensions: {vector.Length}");
    /// </code>
    /// </example>
    public static Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        this IEmbeddingGenerator<string, Embedding<float>> generator,
        string value,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(value);

        return generator.GenerateAsync([value], options, cancellationToken);
    }

    /// <summary>
    /// Generates a single embedding for a string value and returns it directly.
    /// </summary>
    /// <param name="generator">The embedding generator.</param>
    /// <param name="value">The text to generate an embedding for.</param>
    /// <param name="options">Optional embedding generation options.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A single <see cref="Embedding{T}"/> for the input text.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="generator"/> or <paramref name="value"/> is null.
    /// </exception>
    /// <remarks>
    /// This is the simplest way to embed a single piece of text. Unlike
    /// <see cref="GenerateAsync"/>, this method returns the <see cref="Embedding{T}"/>
    /// directly — no collection indexing needed.
    /// </remarks>
    /// <example>
    /// <code>
    /// var embedding = await generator.GenerateEmbeddingAsync("Hello, world!");
    /// float[] vector = embedding.Vector.ToArray();
    /// Console.WriteLine($"Dimensions: {vector.Length}");
    /// </code>
    /// </example>
    public static async Task<Embedding<float>> GenerateEmbeddingAsync(
        this IEmbeddingGenerator<string, Embedding<float>> generator,
        string value,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(value);

        var result = await generator.GenerateAsync([value], options, cancellationToken).ConfigureAwait(false);
        return result[0];
    }
}
