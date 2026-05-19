namespace ElBruno.LocalEmbeddings.Options;

using Microsoft.Extensions.AI;

/// <summary>
/// Configuration options for streaming embedding generation.
/// </summary>
/// <remarks>
/// <para>
/// This options class controls how texts are buffered and batched during streaming
/// embedding generation. It enables fine-tuning of the latency vs. throughput tradeoff.
/// </para>
/// </remarks>
public sealed class StreamingEmbeddingOptions
{
    /// <summary>
    /// Gets or sets the buffer size for batching incoming texts before processing.
    /// Default is 32.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When the buffer reaches this size, a batch is generated and the buffer is flushed.
    /// When the input stream ends, any remaining buffered texts are processed in a final batch.
    /// </para>
    /// <para>
    /// <strong>Performance Tradeoff:</strong>
    /// - Larger values (e.g., 128) → Better GPU utilization, higher throughput, higher latency
    /// - Smaller values (e.g., 4) → Lower latency to first embedding, lower throughput, lower GPU utilization
    /// - Default (32) → Balanced for typical use cases
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if set to a value less than 1.</exception>
    public int BufferSize { get; set; } = 32;

    /// <summary>
    /// Gets or sets the underlying embedding generation options passed to each batch.
    /// Default is null (uses generator defaults).
    /// </summary>
    /// <remarks>
    /// This allows customization of per-batch generation behavior, such as selecting
    /// different models or applying custom generation options via the M.E.AI abstraction.
    /// </remarks>
    public EmbeddingGenerationOptions? EmbeddingOptions { get; set; }
}
