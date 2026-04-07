namespace ElBruno.LocalEmbeddings;

/// <summary>
/// Reports progress during batch embedding generation.
/// </summary>
/// <param name="CompletedItems">Number of items completed so far.</param>
/// <param name="TotalItems">Total number of items to process.</param>
/// <param name="CurrentBatchSize">Size of the current processing batch.</param>
public record EmbeddingProgress(int CompletedItems, int TotalItems, int CurrentBatchSize);
