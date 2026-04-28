using Microsoft.Extensions.VectorData;

namespace RagChat.Models;

/// <summary>
/// Represents a document stored in the vector database with its embedding.
/// </summary>
public sealed class Document
{
    /// <summary>
    /// Gets or sets the unique identifier for this document.
    /// </summary>
    [VectorStoreKey]
    public required string Id { get; init; }

    /// <summary>
    /// Gets or sets the document title or label.
    /// </summary>
    [VectorStoreData]
    public required string Title { get; init; }

    /// <summary>
    /// Gets or sets the document content used for embedding generation.
    /// </summary>
    [VectorStoreData]
    public required string Content { get; init; }

    /// <summary>
    /// Gets or sets the embedding vector for this document.
    /// </summary>
    [VectorStoreVector(384, DistanceFunction = DistanceFunction.CosineSimilarity)]
    public ReadOnlyMemory<float> Vector { get; set; }

    /// <summary>
    /// Gets or sets optional metadata for the document.
    /// </summary>
    [VectorStoreData]
    public string? Category { get; init; }
}
