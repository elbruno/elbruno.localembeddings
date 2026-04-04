namespace ElBruno.LocalEmbeddings.Options;

/// <summary>
/// Configuration options for the embedding cache.
/// </summary>
public sealed class EmbeddingCacheOptions
{
    /// <summary>
    /// Gets or sets whether embedding caching is enabled.
    /// Default is false.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of cached entries.
    /// Default is 10,000.
    /// </summary>
    /// <remarks>
    /// When the cache reaches this size, the oldest entries are evicted using LRU policy.
    /// </remarks>
    public int MaxSize { get; set; } = 10_000;
}
