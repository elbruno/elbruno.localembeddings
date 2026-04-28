namespace ElBruno.LocalEmbeddings.Options;

/// <summary>
/// Configuration options for custom embedder adapters.
/// </summary>
/// <remarks>
/// These options control how the <see cref="ICustomEmbedder"/> adapter behaves when
/// converting custom embeddings to Microsoft.Extensions.AI <see cref="Microsoft.Extensions.AI.Embedding{T}"/> types.
/// </remarks>
public class CustomEmbedderOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to apply L2 normalization to embeddings.
    /// </summary>
    /// <value>
    /// <see langword="true"/> to normalize embeddings to unit length (default);
    /// <see langword="false"/> to use raw embeddings from the custom embedder.
    /// </value>
    /// <remarks>
    /// <para>
    /// L2 normalization ensures that embeddings have unit length, which is required for
    /// cosine similarity calculations and is standard for most embedding models.
    /// </para>
    /// <para>
    /// <strong>When to disable:</strong> If your custom embedder already normalizes embeddings
    /// (check for "normalized" in <see cref="ICustomEmbedder.Capabilities"/>), set this to
    /// <see langword="false"/> to avoid redundant normalization.
    /// </para>
    /// </remarks>
    public bool NormalizeEmbeddings { get; set; } = true;
}
