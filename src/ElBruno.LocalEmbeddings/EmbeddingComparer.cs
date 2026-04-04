using ElBruno.LocalEmbeddings.Extensions;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalEmbeddings;

/// <summary>
/// Compares embeddings across multiple models to evaluate which model best separates similar/dissimilar text pairs.
/// </summary>
/// <remarks>
/// <para>
/// This tool is useful for evaluating different embedding models on your specific dataset.
/// It generates embeddings for the same texts across multiple models and computes pairwise
/// similarity statistics to help you choose the most suitable model.
/// </para>
/// </remarks>
public sealed class EmbeddingComparer
{
    /// <summary>
    /// Result for a single model's evaluation.
    /// </summary>
    /// <param name="ModelName">The name or identifier of the model.</param>
    /// <param name="AverageSimilarity">The average cosine similarity across all text pairs.</param>
    /// <param name="MinSimilarity">The minimum cosine similarity across all text pairs.</param>
    /// <param name="MaxSimilarity">The maximum cosine similarity across all text pairs.</param>
    /// <param name="PairwiseSimilarities">The complete list of pairwise cosine similarities.</param>
    public record ModelComparisonResult(
        string ModelName,
        float AverageSimilarity,
        float MinSimilarity,
        float MaxSimilarity,
        IReadOnlyList<float> PairwiseSimilarities);

    /// <summary>
    /// Full comparison report across all models.
    /// </summary>
    /// <param name="Texts">The texts that were compared.</param>
    /// <param name="Results">The comparison results for each model.</param>
    public record ComparisonReport(
        IReadOnlyList<string> Texts,
        IReadOnlyList<ModelComparisonResult> Results);

    private readonly IReadOnlyList<(string Name, IEmbeddingGenerator<string, Embedding<float>> Generator)> _generators;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddingComparer"/> class.
    /// </summary>
    /// <param name="generators">A collection of named embedding generators to compare.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="generators"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="generators"/> is empty.</exception>
    public EmbeddingComparer(IEnumerable<(string Name, IEmbeddingGenerator<string, Embedding<float>> Generator)> generators)
    {
        ArgumentNullException.ThrowIfNull(generators);

        _generators = generators.ToList();
        if (_generators.Count == 0)
        {
            throw new ArgumentException("At least one generator must be provided.", nameof(generators));
        }
    }

    /// <summary>
    /// Compares how each model embeds the given texts.
    /// </summary>
    /// <param name="texts">The texts to compare.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>
    /// A comparison report containing pairwise similarity statistics for each model.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="texts"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="texts"/> contains fewer than 2 items.</exception>
    /// <remarks>
    /// <para>
    /// This method generates embeddings for all texts using each registered model,
    /// then computes all pairwise cosine similarities. The results help you understand
    /// how well each model distinguishes between different texts in your dataset.
    /// </para>
    /// <para>
    /// The pairwise similarities are computed for all unique pairs (i, j) where i &lt; j.
    /// For n texts, this produces n*(n-1)/2 similarity scores.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var comparer = new EmbeddingComparer(new[]
    /// {
    ///     ("MiniLM", miniLMGenerator),
    ///     ("MPNet", mpNetGenerator)
    /// });
    /// 
    /// var texts = new[] { "cat", "dog", "computer", "laptop" };
    /// var report = await comparer.CompareAsync(texts);
    /// 
    /// foreach (var result in report.Results)
    /// {
    ///     Console.WriteLine($"{result.ModelName}: Avg={result.AverageSimilarity:F3}");
    /// }
    /// </code>
    /// </example>
    public async Task<ComparisonReport> CompareAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(texts);
        if (texts.Count < 2)
        {
            throw new ArgumentException("At least two texts are required for comparison.", nameof(texts));
        }

        var results = new List<ModelComparisonResult>();

        foreach (var (name, generator) in _generators)
        {
            var embeddings = await generator.GenerateAsync(texts, null, cancellationToken).ConfigureAwait(false);
            var metadata = generator.GetService<EmbeddingGeneratorMetadata>();
            var modelName = name ?? metadata?.DefaultModelId ?? "Unknown";
            var similarities = ComputePairwiseSimilarities(embeddings.ToList());

            results.Add(new ModelComparisonResult(
                ModelName: modelName,
                AverageSimilarity: similarities.Count > 0 ? similarities.Average() : 0f,
                MinSimilarity: similarities.Count > 0 ? similarities.Min() : 0f,
                MaxSimilarity: similarities.Count > 0 ? similarities.Max() : 0f,
                PairwiseSimilarities: similarities));
        }

        return new ComparisonReport(texts, results);
    }

    private static List<float> ComputePairwiseSimilarities(IReadOnlyList<Embedding<float>> embeddings)
    {
        var similarities = new List<float>();

        for (int i = 0; i < embeddings.Count; i++)
        {
            for (int j = i + 1; j < embeddings.Count; j++)
            {
                var similarity = embeddings[i].CosineSimilarity(embeddings[j]);
                similarities.Add(similarity);
            }
        }

        return similarities;
    }
}
