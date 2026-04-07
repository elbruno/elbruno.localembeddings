using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;

namespace ElBruno.LocalEmbeddings.VectorData.Extensions;

/// <summary>
/// Extension methods for <see cref="VectorStoreCollection{TKey, TRecord}"/> that integrate embedding generation.
/// </summary>
public static class VectorStoreCollectionExtensions
{
    /// <summary>
    /// Searches the vector store using a text query by automatically generating its embedding.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TRecord">The record type.</typeparam>
    /// <param name="collection">The vector store collection to search.</param>
    /// <param name="generator">The embedding generator to use for converting text to vectors.</param>
    /// <param name="query">The text query to search for.</param>
    /// <param name="top">The maximum number of results to return.</param>
    /// <param name="options">Optional search options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of search results ranked by similarity.</returns>
    /// <exception cref="ArgumentNullException">Thrown when collection, generator, or query is null.</exception>
    /// <exception cref="ArgumentException">Thrown when query is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when top is less than 1.</exception>
    /// <example>
    /// <code>
    /// var generator = serviceProvider.GetRequiredService&lt;IEmbeddingGenerator&lt;string, Embedding&lt;float&gt;&gt;&gt;();
    /// var collection = vectorStore.GetCollection&lt;int, Product&gt;("products");
    /// 
    /// var results = await collection.SearchByTextAsync(
    ///     generator,
    ///     "laptop computer",
    ///     topK: 5);
    /// 
    /// foreach (var result in results)
    /// {
    ///     Console.WriteLine($"{result.Record.Name}: {result.Score:F3}");
    /// }
    /// </code>
    /// </example>
    public static async Task<IReadOnlyList<VectorSearchResult<TRecord>>> SearchByTextAsync<TKey, TRecord>(
        this VectorStoreCollection<TKey, TRecord> collection,
        IEmbeddingGenerator<string, Embedding<float>> generator,
        string query,
        int top = 5,
        VectorSearchOptions<TRecord>? options = null,
        CancellationToken cancellationToken = default)
        where TKey : notnull
        where TRecord : class
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        if (top < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(top), top, "top must be greater than zero.");
        }

        var embedding = await generator.GenerateEmbeddingAsync(query, cancellationToken: cancellationToken);

        var results = new List<VectorSearchResult<TRecord>>();
        await foreach (var result in collection.SearchAsync(embedding, top, options, cancellationToken))
        {
            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// Searches the vector store using multiple text queries by automatically generating their embeddings.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TRecord">The record type.</typeparam>
    /// <param name="collection">The vector store collection to search.</param>
    /// <param name="generator">The embedding generator to use for converting text to vectors.</param>
    /// <param name="queries">The text queries to search for.</param>
    /// <param name="top">The maximum number of results to return per query.</param>
    /// <param name="options">Optional search options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of search result lists, one per query.</returns>
    /// <exception cref="ArgumentNullException">Thrown when collection, generator, or queries is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when top is less than 1.</exception>
    public static async Task<IReadOnlyList<IReadOnlyList<VectorSearchResult<TRecord>>>> SearchByTextBatchAsync<TKey, TRecord>(
        this VectorStoreCollection<TKey, TRecord> collection,
        IEmbeddingGenerator<string, Embedding<float>> generator,
        IEnumerable<string> queries,
        int top = 5,
        VectorSearchOptions<TRecord>? options = null,
        CancellationToken cancellationToken = default)
        where TKey : notnull
        where TRecord : class
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(queries);

        if (top < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(top), top, "top must be greater than zero.");
        }

        var queryList = queries as IList<string> ?? queries.ToList();
        if (queryList.Count == 0)
        {
            return Array.Empty<IReadOnlyList<VectorSearchResult<TRecord>>>();
        }

        var embeddings = await generator.GenerateAsync(queryList, cancellationToken: cancellationToken);

        var results = new List<IReadOnlyList<VectorSearchResult<TRecord>>>(embeddings.Count);

        foreach (var embedding in embeddings)
        {
            var queryResults = new List<VectorSearchResult<TRecord>>();
            await foreach (var result in collection.SearchAsync(embedding, top, options, cancellationToken))
            {
                queryResults.Add(result);
            }

            results.Add(queryResults);
        }

        return results;
    }

    /// <summary>
    /// Upserts a record by automatically generating its embedding from the specified text content.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TRecord">The record type.</typeparam>
    /// <param name="collection">The vector store collection.</param>
    /// <param name="generator">The embedding generator to use for converting text to vectors.</param>
    /// <param name="record">The record to upsert.</param>
    /// <param name="textSelector">A function that extracts the text content from the record for embedding generation.</param>
    /// <param name="vectorSetter">An action that sets the generated embedding on the record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when collection, generator, record, textSelector, or vectorSetter is null.</exception>
    /// <example>
    /// <code>
    /// var product = new Product { Id = 1, Name = "Laptop", Description = "High-performance laptop" };
    /// 
    /// await collection.UpsertWithEmbeddingAsync(
    ///     generator,
    ///     product,
    ///     p => $"{p.Name} {p.Description}",
    ///     (p, embedding) => p.Vector = embedding.Vector);
    /// </code>
    /// </example>
    public static async Task UpsertWithEmbeddingAsync<TKey, TRecord>(
        this VectorStoreCollection<TKey, TRecord> collection,
        IEmbeddingGenerator<string, Embedding<float>> generator,
        TRecord record,
        Func<TRecord, string> textSelector,
        Action<TRecord, Embedding<float>> vectorSetter,
        CancellationToken cancellationToken = default)
        where TKey : notnull
        where TRecord : class
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(textSelector);
        ArgumentNullException.ThrowIfNull(vectorSetter);

        var text = textSelector(record);
        var embedding = await generator.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken);
        vectorSetter(record, embedding);

        await collection.UpsertAsync(record, cancellationToken);
    }

    /// <summary>
    /// Upserts multiple records by automatically generating embeddings from the specified text content.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TRecord">The record type.</typeparam>
    /// <param name="collection">The vector store collection.</param>
    /// <param name="generator">The embedding generator to use for converting text to vectors.</param>
    /// <param name="records">The records to upsert.</param>
    /// <param name="textSelector">A function that extracts the text content from each record for embedding generation.</param>
    /// <param name="vectorSetter">An action that sets the generated embedding on each record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when collection, generator, records, textSelector, or vectorSetter is null.</exception>
    public static async Task UpsertBatchWithEmbeddingAsync<TKey, TRecord>(
        this VectorStoreCollection<TKey, TRecord> collection,
        IEmbeddingGenerator<string, Embedding<float>> generator,
        IEnumerable<TRecord> records,
        Func<TRecord, string> textSelector,
        Action<TRecord, Embedding<float>> vectorSetter,
        CancellationToken cancellationToken = default)
        where TKey : notnull
        where TRecord : class
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(textSelector);
        ArgumentNullException.ThrowIfNull(vectorSetter);

        var recordList = records as IList<TRecord> ?? records.ToList();
        if (recordList.Count == 0)
        {
            return;
        }

        var texts = recordList.Select(textSelector).ToList();
        var embeddings = await generator.GenerateAsync(texts, cancellationToken: cancellationToken);

        for (var i = 0; i < recordList.Count; i++)
        {
            vectorSetter(recordList[i], embeddings[i]);
        }

        await collection.UpsertAsync(recordList, cancellationToken);
    }
}
