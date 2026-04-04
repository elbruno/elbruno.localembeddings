using Microsoft.Extensions.AI;
using Microsoft.ML.OnnxRuntime;

namespace ElBruno.LocalEmbeddings.Middleware;

/// <summary>
/// Middleware that retries embedding generation on transient failures.
/// </summary>
public sealed class RetryEmbeddingMiddleware : DelegatingEmbeddingGenerator<string, Embedding<float>>
{
    private readonly int _maxRetries;
    private readonly TimeSpan _baseDelay;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryEmbeddingMiddleware"/> class.
    /// </summary>
    /// <param name="innerGenerator">The inner embedding generator to wrap.</param>
    /// <param name="maxRetries">Maximum number of retry attempts. Default is 3.</param>
    /// <param name="baseDelay">Base delay between retries. Default is 200ms. Uses exponential backoff.</param>
    public RetryEmbeddingMiddleware(
        IEmbeddingGenerator<string, Embedding<float>> innerGenerator,
        int maxRetries = 3,
        TimeSpan? baseDelay = null)
        : base(innerGenerator)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRetries);
        
        _maxRetries = maxRetries;
        _baseDelay = baseDelay ?? TimeSpan.FromMilliseconds(200);
    }

    /// <inheritdoc/>
    public override async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var attempt = 0;
        Exception? lastException = null;

        while (attempt <= _maxRetries)
        {
            try
            {
                return await base.GenerateAsync(values, options, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsRetriableException(ex) && attempt < _maxRetries)
            {
                lastException = ex;
                attempt++;
                
                var delay = TimeSpan.FromMilliseconds(_baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        throw lastException ?? new InvalidOperationException("Retry loop completed without exception");
    }

    private static bool IsRetriableException(Exception ex)
    {
        return ex is OnnxRuntimeException or IOException;
    }
}
