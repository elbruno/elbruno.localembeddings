namespace RagChat.VectorStore;

/// <summary>
/// Represents a document stored in the vector database with its embedding.
/// </summary>
public sealed class Document
{
    /// <summary>
    /// Gets or sets the unique identifier for this document.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets or sets the document title or label.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets or sets the document content used for embedding generation.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets or sets the embedding vector for this document.
    /// </summary>
    public float[]? Embedding { get; set; }

    /// <summary>
    /// Gets or sets optional metadata for the document.
    /// </summary>
    public string? Category { get; init; }
}
