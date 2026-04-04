using System.Runtime.CompilerServices;
using ElBruno.LocalEmbeddings.Extensions;
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
    /// This is the simplest way to embed a single piece of text. Unlike the overload
    /// that takes a single string, this method returns the <see cref="Embedding{T}"/>
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

    /// <summary>
    /// Finds the closest text matches from a corpus for the specified query.
    /// </summary>
    /// <param name="generator">The embedding generator.</param>
    /// <param name="query">The user query text.</param>
    /// <param name="corpus">The searchable text corpus.</param>
    /// <param name="corpusEmbeddings">Optional precomputed embeddings aligned with <paramref name="corpus"/>.</param>
    /// <param name="topK">The maximum number of results to return. Must be greater than zero.</param>
    /// <param name="minScore">Optional minimum cosine similarity threshold.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The closest matches ordered by descending score and then by ascending index.</returns>
    /// <remarks>
    /// If <paramref name="corpusEmbeddings"/> is <see langword="null"/>, embeddings are generated in batch for
    /// the full corpus before searching. This method performs a linear scan with complexity O(n).
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="generator"/>, <paramref name="query"/>, or <paramref name="corpus"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="query"/> is empty or when corpus/embedding counts differ.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="topK"/> is less than 1.</exception>
    /// <example>
    /// <code>
    /// var corpus = new[] { "C# for .NET", "JavaScript for browsers", "Swift for iOS" };
    /// var results = await generator.FindClosestAsync(
    ///     "best language for web apps",
    ///     corpus,
    ///     topK: 2,
    ///     minScore: 0.2f);
    /// </code>
    /// </example>
    public static async Task<IReadOnlyList<SemanticSearchResult>> FindClosestAsync(
        this IEmbeddingGenerator<string, Embedding<float>> generator,
        string query,
        IReadOnlyList<string> corpus,
        IReadOnlyList<Embedding<float>>? corpusEmbeddings = null,
        int topK = 3,
        float? minScore = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(corpus);

        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query cannot be null, empty, or whitespace.", nameof(query));
        }

        if (topK < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(topK), topK, "topK must be greater than zero.");
        }

        if (corpus.Count == 0)
        {
            return [];
        }

        IReadOnlyList<Embedding<float>> resolvedCorpusEmbeddings = corpusEmbeddings ??
            await generator.GenerateAsync(corpus, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (resolvedCorpusEmbeddings.Count != corpus.Count)
        {
            throw new ArgumentException(
                "Corpus and corpusEmbeddings must contain the same number of items.",
                nameof(corpusEmbeddings));
        }

        var queryEmbedding = await generator.GenerateEmbeddingAsync(query, cancellationToken: cancellationToken).ConfigureAwait(false);
        var closest = queryEmbedding.FindClosest(resolvedCorpusEmbeddings, topK, minScore);

        return closest
            .Select(result => new SemanticSearchResult(corpus[result.Index], result.Index, result.Score))
            .ToList();
    }

    /// <summary>
    /// Finds the closest matches from a typed corpus for the specified query.
    /// </summary>
    /// <typeparam name="T">The corpus item type.</typeparam>
    /// <param name="generator">The embedding generator.</param>
    /// <param name="query">The user query text.</param>
    /// <param name="corpus">The searchable corpus.</param>
    /// <param name="textSelector">Selector that extracts searchable text from each corpus item.</param>
    /// <param name="corpusEmbeddings">Optional precomputed embeddings aligned with <paramref name="corpus"/>.</param>
    /// <param name="topK">The maximum number of results to return. Must be greater than zero.</param>
    /// <param name="minScore">Optional minimum cosine similarity threshold.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The closest matches ordered by descending score and then by ascending index.</returns>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
    /// <exception cref="ArgumentException">Thrown when selected text is null or whitespace for any corpus item.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="topK"/> is less than 1.</exception>
    public static Task<IReadOnlyList<SemanticSearchResult>> FindClosestAsync<T>(
        this IEmbeddingGenerator<string, Embedding<float>> generator,
        string query,
        IReadOnlyList<T> corpus,
        Func<T, string> textSelector,
        IReadOnlyList<Embedding<float>>? corpusEmbeddings = null,
        int topK = 3,
        float? minScore = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentNullException.ThrowIfNull(textSelector);

        var extractedCorpus = corpus
            .Select((item, index) =>
            {
                var text = textSelector(item);
                if (string.IsNullOrWhiteSpace(text))
                {
                    throw new ArgumentException($"Corpus item at index {index} produced null or whitespace text.", nameof(textSelector));
                }

                return text;
            })
            .ToList();

        return generator.FindClosestAsync(query, extractedCorpus, corpusEmbeddings, topK, minScore, cancellationToken);
    }

    /// <summary>
    /// Generates embeddings for a batch of strings with progress reporting.
    /// </summary>
    /// <param name="generator">The embedding generator.</param>
    /// <param name="values">The texts to generate embeddings for.</param>
    /// <param name="progress">Progress reporter that receives updates after each batch is processed.</param>
    /// <param name="batchSize">Number of items to process in each batch. Default is 32.</param>
    /// <param name="options">Optional embedding generation options.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="GeneratedEmbeddings{TEmbedding}"/> containing all embeddings in the same order as the input.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="generator"/>, <paramref name="values"/>, or <paramref name="progress"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="batchSize"/> is less than 1.
    /// </exception>
    /// <remarks>
    /// This method splits the input into batches and processes them sequentially,
    /// reporting progress after each batch completes. This is useful for monitoring
    /// long-running embedding operations on large datasets.
    /// </remarks>
    /// <example>
    /// <code>
    /// var progress = new Progress&lt;EmbeddingProgress&gt;(p =>
    ///     Console.WriteLine($"{p.CompletedItems}/{p.TotalItems} items completed"));
    /// 
    /// var texts = new[] { "text1", "text2", ... };
    /// var result = await generator.GenerateAsync(texts, progress, batchSize: 32);
    /// </code>
    /// </example>
    public static async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        this IEmbeddingGenerator<string, Embedding<float>> generator,
        IEnumerable<string> values,
        IProgress<EmbeddingProgress> progress,
        int batchSize = 32,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(progress);

        if (batchSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), batchSize, "Batch size must be greater than zero.");
        }

        var valuesList = values.ToList();
        var totalItems = valuesList.Count;

        if (totalItems == 0)
        {
            return new GeneratedEmbeddings<Embedding<float>>([]);
        }

        var allEmbeddings = new List<Embedding<float>>(totalItems);
        var completedItems = 0;

        foreach (var batch in valuesList.Chunk(batchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batchResult = await generator.GenerateAsync(batch, options, cancellationToken).ConfigureAwait(false);
            allEmbeddings.AddRange(batchResult);

            completedItems += batch.Length;
            progress.Report(new EmbeddingProgress(completedItems, totalItems, batch.Length));
        }

        return new GeneratedEmbeddings<Embedding<float>>(allEmbeddings);
    }

    /// <summary>
    /// Generates embeddings for a batch of strings and yields them as they become available.
    /// </summary>
    /// <param name="generator">The embedding generator.</param>
    /// <param name="values">The texts to generate embeddings for.</param>
    /// <param name="batchSize">Number of items to process in each batch. Default is 32.</param>
    /// <param name="options">Optional embedding generation options.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>
    /// An async enumerable that yields <see cref="Embedding{T}"/> instances in the same order as the input.
    /// Each batch is processed sequentially, and embeddings are yielded as soon as each batch completes.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="generator"/> or <paramref name="values"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="batchSize"/> is less than 1.
    /// </exception>
    /// <remarks>
    /// This method enables streaming processing of large datasets without waiting for all embeddings
    /// to complete. Embeddings are yielded in input order as each batch is processed.
    /// </remarks>
    /// <example>
    /// <code>
    /// var texts = new[] { "text1", "text2", ... };
    /// await foreach (var embedding in generator.GenerateStreamingAsync(texts, batchSize: 32))
    /// {
    ///     ProcessEmbedding(embedding);
    /// }
    /// </code>
    /// </example>
    public static async IAsyncEnumerable<Embedding<float>> GenerateStreamingAsync(
        this IEmbeddingGenerator<string, Embedding<float>> generator,
        IEnumerable<string> values,
        int batchSize = 32,
        EmbeddingGenerationOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(values);

        if (batchSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), batchSize, "Batch size must be greater than zero.");
        }

        var valuesList = values.ToList();

        foreach (var batch in valuesList.Chunk(batchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batchResult = await generator.GenerateAsync(batch, options, cancellationToken).ConfigureAwait(false);

            foreach (var embedding in batchResult)
            {
                yield return embedding;
            }
        }
    }
}
