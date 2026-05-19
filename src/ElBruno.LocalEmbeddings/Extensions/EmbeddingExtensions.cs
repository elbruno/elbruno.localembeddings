using TensorPrimitives = System.Numerics.Tensors.TensorPrimitives;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalEmbeddings.Extensions;

/// <summary>
/// Extension methods for working with embeddings, including similarity calculations and search operations.
/// </summary>
public static class EmbeddingExtensions
{
    /// <summary>
    /// Calculates the cosine similarity between two vectors.
    /// </summary>
    /// <param name="a">The first vector.</param>
    /// <param name="b">The second vector.</param>
    /// <returns>
    /// A value between -1 and 1, where 1 indicates identical direction,
    /// 0 indicates orthogonality, and -1 indicates opposite direction.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when vectors have different lengths.</exception>
    /// <remarks>
    /// <para>
    /// Cosine similarity measures the angle between two vectors, making it useful for
    /// comparing embeddings regardless of their magnitude. For normalized embeddings,
    /// this is equivalent to the dot product.
    /// </para>
    /// <para>
    /// This method uses <see cref="System.Numerics.Tensors.TensorPrimitives.CosineSimilarity"/>
    /// for SIMD acceleration on supported platforms, providing 2-3x performance improvement
    /// for typical embedding dimensions (384-dim and 768-dim vectors).
    /// </para>
    /// </remarks>
    public static float CosineSimilarity(this ReadOnlyMemory<float> a, ReadOnlyMemory<float> b)
    {
        var spanA = a.Span;
        var spanB = b.Span;

        if (spanA.Length != spanB.Length)
        {
            throw new ArgumentException("Vectors must have the same length.", nameof(b));
        }

        return TensorPrimitives.CosineSimilarity(spanA, spanB);
    }

    /// <summary>
    /// Calculates the cosine similarity between two embeddings.
    /// </summary>
    /// <param name="a">The first embedding.</param>
    /// <param name="b">The second embedding.</param>
    /// <returns>
    /// A value between -1 and 1, where 1 indicates identical direction,
    /// 0 indicates orthogonality, and -1 indicates opposite direction.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when either embedding is null.</exception>
    /// <exception cref="ArgumentException">Thrown when embeddings have different dimensions.</exception>
    public static float CosineSimilarity(this Embedding<float> a, Embedding<float> b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        return a.Vector.CosineSimilarity(b.Vector);
    }

    /// <summary>
    /// Calculates a full all-pairs cosine similarity matrix between two embedding collections.
    /// </summary>
    /// <param name="embeddings1">The first embedding collection (rows).</param>
    /// <param name="embeddings2">The second embedding collection (columns).</param>
    /// <returns>
    /// A matrix with shape [embeddings1.Count, embeddings2.Count], where each cell [i, j]
    /// contains the cosine similarity between embeddings1[i] and embeddings2[j].
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when either collection is null.</exception>
    /// <exception cref="ArgumentException">Thrown when embedding dimensions do not match.</exception>
    /// <remarks>
    /// This is similar to Sentence Transformers <c>model.similarity(a, b)</c> behavior,
    /// returning scores for all combinations of vectors from both collections.
    /// </remarks>
    public static float[,] Similarity(
        this IEnumerable<Embedding<float>> embeddings1,
        IEnumerable<Embedding<float>> embeddings2)
    {
        ArgumentNullException.ThrowIfNull(embeddings1);
        ArgumentNullException.ThrowIfNull(embeddings2);

        var left = embeddings1.ToList();
        var right = embeddings2.ToList();
        var similarities = new float[left.Count, right.Count];

        for (int i = 0; i < left.Count; i++)
        {
            for (int j = 0; j < right.Count; j++)
            {
                similarities[i, j] = left[i].CosineSimilarity(right[j]);
            }
        }

        return similarities;
    }

    /// <summary>
    /// Calculates an all-pairs cosine similarity matrix within a single embedding collection.
    /// </summary>
    /// <param name="embeddings">The embedding collection.</param>
    /// <returns>
    /// A square matrix with shape [count, count] containing all-pairs cosine similarity scores.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when the collection is null.</exception>
    public static float[,] Similarity(this IEnumerable<Embedding<float>> embeddings)
    {
        ArgumentNullException.ThrowIfNull(embeddings);
        return embeddings.Similarity(embeddings);
    }

    /// <summary>
    /// Finds the closest matching items to a query embedding based on cosine similarity.
    /// </summary>
    /// <typeparam name="T">The type of item associated with each embedding.</typeparam>
    /// <param name="items">A collection of items with their associated embeddings.</param>
    /// <param name="query">The query embedding to compare against.</param>
    /// <param name="topK">The maximum number of results to return. Default is 5.</param>
    /// <param name="minScore">The minimum similarity score threshold. Items below this score are excluded. Default is 0.0.</param>
    /// <returns>
    /// A list of tuples containing matching items and their similarity scores,
    /// ordered by descending similarity.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when items or query is null.</exception>
    /// <remarks>
    /// This method is useful for semantic search scenarios where you want to find
    /// the most similar items in a collection to a given query.
    /// </remarks>
    /// <example>
    /// <code>
    /// var documents = new List&lt;(string Text, Embedding&lt;float&gt; Embedding)&gt;
    /// {
    ///     ("Hello world", embedding1),
    ///     ("Goodbye world", embedding2)
    /// };
    /// var queryEmbedding = await generator.GenerateAsync(["search query"]);
    /// var results = documents.FindClosest(queryEmbedding[0], topK: 3, minScore: 0.5f);
    /// </code>
    /// </example>
    public static List<(T Item, float Score)> FindClosest<T>(
        this IEnumerable<(T Item, Embedding<float> Embedding)> items,
        Embedding<float> query,
        int topK = 5,
        float minScore = 0.0f)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(query);

        // O(n log k) partial sort using a min-heap of size topK
        var heap = new PriorityQueue<T, float>(topK);
        foreach (var (item, embedding) in items)
        {
            float score = query.CosineSimilarity(embedding);
            if (score < minScore) continue;

            if (heap.Count < topK)
            {
                heap.Enqueue(item, score);
            }
            else if (heap.TryPeek(out _, out float lowestScore) && score > lowestScore)
            {
                heap.DequeueEnqueue(item, score);
            }
        }

        var results = new List<(T Item, float Score)>(heap.Count);
        while (heap.TryDequeue(out var item2, out float score2))
            results.Add((item2, score2));
        results.Reverse(); // min-heap gives ascending order; we want descending
        return results;
    }

    /// <summary>
    /// Finds the closest embeddings to the query embedding using cosine similarity.
    /// </summary>
    /// <param name="queryEmbedding">The query embedding.</param>
    /// <param name="corpusEmbeddings">The corpus embeddings to compare against.</param>
    /// <param name="topK">The maximum number of results to return. Must be greater than zero.</param>
    /// <param name="minScore">Optional minimum cosine similarity threshold.</param>
    /// <returns>
    /// A list of (Index, Score) tuples ordered by descending score and then by ascending index.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="queryEmbedding"/> or <paramref name="corpusEmbeddings"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="topK"/> is less than 1.</exception>
    /// <remarks>
    /// This operation performs a linear scan over the corpus, with time complexity O(n).
    /// </remarks>
    public static List<(int Index, float Score)> FindClosest(
        this Embedding<float> queryEmbedding,
        IReadOnlyList<Embedding<float>> corpusEmbeddings,
        int topK = 3,
        float? minScore = null)
    {
        ArgumentNullException.ThrowIfNull(queryEmbedding);
        ArgumentNullException.ThrowIfNull(corpusEmbeddings);

        if (topK < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(topK), topK, "topK must be greater than zero.");
        }

        if (corpusEmbeddings.Count == 0)
        {
            return [];
        }

        // O(n log k) partial sort using a min-heap of size topK
        var heap = new PriorityQueue<int, float>(topK);
        for (int i = 0; i < corpusEmbeddings.Count; i++)
        {
            float score = queryEmbedding.CosineSimilarity(corpusEmbeddings[i]);
            if (minScore is not null && score < minScore.Value) continue;

            if (heap.Count < topK)
            {
                heap.Enqueue(i, score);
            }
            else if (heap.TryPeek(out _, out float lowestScore) && score > lowestScore)
            {
                heap.DequeueEnqueue(i, score);
            }
        }

        var results = new List<(int Index, float Score)>(heap.Count);
        while (heap.TryDequeue(out int idx, out float s))
            results.Add((idx, s));

        // Sort topK results by descending score then ascending index (matches original behavior)
        results.Sort((x, y) =>
        {
            int cmp = y.Score.CompareTo(x.Score);
            return cmp != 0 ? cmp : x.Index.CompareTo(y.Index);
        });
        return results;
    }
}
