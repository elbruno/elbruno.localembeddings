namespace ElBruno.LocalEmbeddings.Extensions;

using System.Runtime.CompilerServices;
using ElBruno.LocalEmbeddings.Options;
using Microsoft.Extensions.AI;

/// <summary>
/// Extension methods for streaming embedding generation.
/// </summary>
/// <remarks>
/// <para>
/// These methods enable incremental, buffered embedding generation for large datasets
/// without exhausting memory. Texts are buffered to a configurable size and processed
/// in batches; embeddings are yielded as soon as each batch completes.
/// </para>
/// <para>
/// This is ideal for production-scale RAG pipelines processing 100K+ vectors or
/// long-lived streams (e.g., message queues, file readers, API endpoints).
/// </para>
/// </remarks>
public static class StreamingExtensions
{
    /// <summary>
    /// Generates embeddings for an asynchronous stream of texts with buffering and batching.
    /// </summary>
    /// <param name="generator">The embedding generator.</param>
    /// <param name="texts">An async enumerable stream of texts to embed.</param>
    /// <param name="options">Configuration for streaming (buffer size, embedding options). If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>
    /// An async enumerable that yields <see cref="Embedding{T}"/> instances in input order
    /// as each batch completes. Embeddings are yielded immediately after inference, not accumulated.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="generator"/> or <paramref name="texts"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when options.BufferSize is less than 1.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is signaled.</exception>
    /// <remarks>
    /// <para>
    /// <strong>Buffering Strategy:</strong>
    /// Texts are accumulated in a buffer up to the configured size (default 32). When the buffer
    /// is full, it is flushed: texts are tokenized, embedded via ONNX inference, and embeddings
    /// are yielded. This process repeats until the input stream ends, at which point any remaining
    /// texts are processed in a final batch.
    /// </para>
    /// <para>
    /// <strong>Memory Profile:</strong>
    /// Memory usage is O(buffer_size + model_size), independent of total input count.
    /// This enables processing infinite streams or datasets much larger than available RAM.
    /// </para>
    /// <para>
    /// <strong>Concurrency and Thread Safety:</strong>
    /// This method is async-safe and can be safely called from UI threads or other async contexts.
    /// The underlying <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> batch operation is thread-safe
    /// for concurrent calls.
    /// </para>
    /// <para>
    /// <strong>Error Handling:</strong>
    /// If the input enumeration fails, the exception is propagated immediately and the remaining
    /// buffer is not flushed. If batch generation fails, the exception is propagated after already-yielded
    /// embeddings remain valid. Cancellation requests propagate as <see cref="OperationCanceledException"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// Basic usage with default options:
    /// <code>
    /// var texts = ProduceTextStreamAsync();
    /// await foreach (var embedding in generator.GenerateStreamingAsync(texts))
    /// {
    ///     // Process embedding (approximately 32 at a time)
    ///     await Store(embedding);
    /// }
    /// </code>
    /// With custom buffer size:
    /// <code>
    /// var options = new StreamingEmbeddingOptions { BufferSize = 64 };
    /// await foreach (var embedding in generator.GenerateStreamingAsync(texts, options))
    /// {
    ///     // Larger batches → higher throughput, higher latency
    /// }
    /// </code>
    /// </example>
    public static async IAsyncEnumerable<Embedding<float>> GenerateStreamingAsync(
        this IEmbeddingGenerator<string, Embedding<float>> generator,
        IAsyncEnumerable<string> texts,
        StreamingEmbeddingOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(texts);

        var opts = options ?? new StreamingEmbeddingOptions();

        if (opts.BufferSize < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                opts.BufferSize,
                "BufferSize must be greater than zero.");
        }

        var buffer = new List<string>(capacity: opts.BufferSize);

        // Enumerate input stream asynchronously
        await foreach (var text in texts.WithCancellation(cancellationToken))
        {
            // Check cancellation at each iteration
            cancellationToken.ThrowIfCancellationRequested();

            buffer.Add(text);

            // Flush buffer when it reaches target size
            if (buffer.Count >= opts.BufferSize)
            {
                var batch = buffer.ToList();
                buffer.Clear();

                // Generate embeddings for this batch
                var embeddings = await generator.GenerateAsync(
                    batch,
                    opts.EmbeddingOptions,
                    cancellationToken).ConfigureAwait(false);

                // Yield each embedding as it becomes available
                foreach (var embedding in embeddings)
                {
                    yield return embedding;
                }
            }
        }

        // Stream ended: flush any remaining buffered texts
        if (buffer.Count > 0)
        {
            var embeddings = await generator.GenerateAsync(
                buffer,
                opts.EmbeddingOptions,
                cancellationToken).ConfigureAwait(false);

            foreach (var embedding in embeddings)
            {
                yield return embedding;
            }
        }
    }

    /// <summary>
    /// Generates embeddings for an asynchronous stream of texts with buffering and progress reporting.
    /// </summary>
    /// <param name="generator">The embedding generator.</param>
    /// <param name="texts">An async enumerable stream of texts to embed.</param>
    /// <param name="progress">A progress reporter that receives updates after each batch.</param>
    /// <param name="options">Configuration for streaming (buffer size, embedding options). If null, defaults are used.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>
    /// An async enumerable that yields <see cref="Embedding{T}"/> instances in input order
    /// as each batch completes.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is signaled.</exception>
    /// <remarks>
    /// <para>
    /// This overload is identical to <see cref="GenerateStreamingAsync(IEmbeddingGenerator{String, Embedding{Single}}, IAsyncEnumerable{String}, StreamingEmbeddingOptions, CancellationToken)"/>
    /// except it additionally reports progress after each batch is processed.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var progress = new Progress&lt;EmbeddingProgress&gt;(p =>
    ///     Console.WriteLine($"{p.CompletedItems}/{p.TotalItems} embeddings generated"));
    /// 
    /// await foreach (var embedding in generator.GenerateStreamingAsync(texts, progress))
    /// {
    ///     await Store(embedding);
    /// }
    /// </code>
    /// </example>
    public static async IAsyncEnumerable<Embedding<float>> GenerateStreamingAsync(
        this IEmbeddingGenerator<string, Embedding<float>> generator,
        IAsyncEnumerable<string> texts,
        IProgress<EmbeddingProgress> progress,
        StreamingEmbeddingOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(progress);

        var opts = options ?? new StreamingEmbeddingOptions();

        if (opts.BufferSize < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                opts.BufferSize,
                "BufferSize must be greater than zero.");
        }

        var buffer = new List<string>(capacity: opts.BufferSize);
        int totalProcessed = 0;

        await foreach (var text in texts.WithCancellation(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            buffer.Add(text);

            if (buffer.Count >= opts.BufferSize)
            {
                var batch = buffer.ToList();
                var batchSize = batch.Count;
                buffer.Clear();

                var embeddings = await generator.GenerateAsync(
                    batch,
                    opts.EmbeddingOptions,
                    cancellationToken).ConfigureAwait(false);

                foreach (var embedding in embeddings)
                {
                    totalProcessed++;
                    yield return embedding;
                }

                // Report progress after batch
                progress.Report(new EmbeddingProgress(totalProcessed, -1, batchSize));
            }
        }

        if (buffer.Count > 0)
        {
            var batchSize = buffer.Count;
            var embeddings = await generator.GenerateAsync(
                buffer,
                opts.EmbeddingOptions,
                cancellationToken).ConfigureAwait(false);

            foreach (var embedding in embeddings)
            {
                totalProcessed++;
                yield return embedding;
            }

            // Final progress report
            progress.Report(new EmbeddingProgress(totalProcessed, -1, batchSize));
        }
    }
}
