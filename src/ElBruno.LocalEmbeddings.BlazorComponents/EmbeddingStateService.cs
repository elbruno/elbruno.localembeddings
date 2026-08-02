using Microsoft.Extensions.AI;

namespace ElBruno.LocalEmbeddings.BlazorComponents;

/// <summary>
/// Scoped service that tracks embedding model state and exposes helpers used by
/// the BlazorComponents library. Register with <see cref="ServiceCollectionExtensions.AddLocalEmbeddingsBlazorComponents"/>.
/// </summary>
public sealed class EmbeddingStateService
{
    private static readonly List<EmbeddingModelInfo> _wellKnownModels =
    [
        new EmbeddingModelInfo
        {
            ModelId      = "sentence-transformers/all-MiniLM-L6-v2",
            DisplayName  = "all-MiniLM-L6-v2",
            Dimensions   = 384,
            SizeMb       = 23,
            Language     = "English",
            Description  = "Fast, compact general-purpose English embeddings."
        },
        new EmbeddingModelInfo
        {
            ModelId      = "sentence-transformers/all-mpnet-base-v2",
            DisplayName  = "all-mpnet-base-v2",
            Dimensions   = 768,
            SizeMb       = 438,
            Language     = "English",
            Description  = "High-quality semantic sentence embeddings."
        },
        new EmbeddingModelInfo
        {
            ModelId      = "sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2",
            DisplayName  = "multilingual-MiniLM-L12-v2",
            Dimensions   = 384,
            SizeMb       = 118,
            Language     = "Multilingual",
            Description  = "50+ language support in a compact model."
        },
        new EmbeddingModelInfo
        {
            ModelId      = "BAAI/bge-small-en-v1.5",
            DisplayName  = "bge-small-en-v1.5",
            Dimensions   = 384,
            SizeMb       = 33,
            Language     = "English",
            Description  = "BAAI general embeddings, small & fast."
        },
        new EmbeddingModelInfo
        {
            ModelId      = "BAAI/bge-m3",
            DisplayName  = "bge-m3",
            Dimensions   = 1024,
            SizeMb       = 570,
            Language     = "Multilingual",
            Description  = "State-of-the-art multilingual dense retrieval model."
        },
        new EmbeddingModelInfo
        {
            ModelId      = "intfloat/e5-small-v2",
            DisplayName  = "e5-small-v2",
            Dimensions   = 384,
            SizeMb       = 33,
            Language     = "English",
            Description  = "Small, efficient E5 embeddings."
        },
    ];

    private string? _selectedModelId;

    /// <summary>All well-known embedding models tracked by this service.</summary>
    public IReadOnlyList<EmbeddingModelInfo> Models => _wellKnownModels;

    /// <summary>Currently active model ID.</summary>
    public string? SelectedModelId
    {
        get => _selectedModelId;
        set
        {
            if (_selectedModelId != value)
            {
                _selectedModelId = value;
                SelectedModelChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>Raised when <see cref="SelectedModelId"/> changes.</summary>
    public event EventHandler? SelectedModelChanged;

    /// <summary>Returns the currently selected <see cref="EmbeddingModelInfo"/>, or null.</summary>
    public EmbeddingModelInfo? SelectedModel =>
        _selectedModelId is null ? null : _wellKnownModels.Find(m => m.ModelId == _selectedModelId);

    /// <summary>
    /// Computes cosine similarity between two float vectors.
    /// Returns a value in [-1, 1].
    /// </summary>
    public static float CosineSimilarity(float[] a, float[] b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (a.Length != b.Length)
            throw new ArgumentException("Vectors must have the same length.");

        float dot = 0f, normA = 0f, normB = 0f;
        for (int i = 0; i < a.Length; i++)
        {
            dot   += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0f || normB == 0f)
            return 0f;

        return dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }

    /// <summary>
    /// Generates embeddings for a list of texts using the provided generator.
    /// Returns a parallel list of float arrays.
    /// </summary>
    public static async Task<float[][]> GenerateEmbeddingsAsync(
        IEmbeddingGenerator<string, Embedding<float>> generator,
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(texts);

        var list = texts.ToList();
        var result = await generator.GenerateAsync(list, cancellationToken: cancellationToken).ConfigureAwait(false);
        return [.. result.Select(e => e.Vector.ToArray())];
    }
}
