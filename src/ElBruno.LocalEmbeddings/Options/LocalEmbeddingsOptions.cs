namespace ElBruno.LocalEmbeddings.Options;

/// <summary>
/// Configuration options for <see cref="LocalEmbeddingGenerator"/>.
/// </summary>
public sealed class LocalEmbeddingsOptions
{
    /// <summary>
    /// Gets or sets the HuggingFace model name to use.
    /// Default is "sentence-transformers/all-MiniLM-L6-v2".
    /// </summary>
    public string ModelName { get; set; } = "sentence-transformers/all-MiniLM-L6-v2";

    /// <summary>
    /// Gets or sets the path to a local model directory.
    /// If specified, the model will be loaded from this path instead of being downloaded.
    /// </summary>
    public string? ModelPath { get; set; }

    /// <summary>
    /// Gets or sets the directory where models are cached.
    /// If null, uses the default cache directory.
    /// </summary>
    public string? CacheDirectory { get; set; }

    /// <summary>
    /// Gets or sets the maximum sequence length for tokenization.
    /// Default is 512.
    /// </summary>
    public int MaxSequenceLength { get; set; } = 512;

    /// <summary>
    /// Gets or sets whether to ensure the model is downloaded on startup.
    /// Default is true. Set to false if <see cref="ModelPath"/> is specified.
    /// </summary>
    public bool EnsureModelDownloaded { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to L2-normalize embedding vectors to unit length.
    /// Default is false.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When enabled, embeddings are normalized to have a magnitude of 1 (unit vectors).
    /// This matches the default behavior of sentence-transformers in Python.
    /// </para>
    /// <para>
    /// Normalized embeddings have the property that cosine similarity equals the dot product,
    /// which can simplify and accelerate similarity computations.
    /// </para>
    /// </remarks>
    public bool NormalizeEmbeddings { get; set; } = false;
}
