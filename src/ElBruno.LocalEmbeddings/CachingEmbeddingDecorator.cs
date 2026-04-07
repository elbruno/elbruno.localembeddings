using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using ElBruno.LocalEmbeddings.Options;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalEmbeddings;

/// <summary>
/// Decorator that adds LRU caching to an existing <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/>.
/// </summary>
/// <remarks>
/// <para>
/// This decorator caches embeddings keyed by the SHA-256 hash of the input text.
/// When the cache reaches <see cref="EmbeddingCacheOptions.MaxSize"/>, the oldest
/// entries are evicted using a Least Recently Used (LRU) policy.
/// </para>
/// <para>
/// For batch operations, the decorator intelligently checks the cache for each input
/// and only sends uncached items to the inner generator, then merges the results.
/// </para>
/// </remarks>
public sealed class CachingEmbeddingDecorator : IEmbeddingGenerator<string, Embedding<float>>, IDisposable, IAsyncDisposable
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _innerGenerator;
    private readonly ConcurrentDictionary<string, Embedding<float>> _cache;
    private readonly ConcurrentQueue<string> _lruQueue;
    private readonly int _maxSize;
    private readonly object _evictionLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CachingEmbeddingDecorator"/> class.
    /// </summary>
    /// <param name="innerGenerator">The inner embedding generator to wrap.</param>
    /// <param name="maxSize">The maximum number of cached entries.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="innerGenerator"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxSize"/> is less than 1.</exception>
    public CachingEmbeddingDecorator(
        IEmbeddingGenerator<string, Embedding<float>> innerGenerator,
        int maxSize = 10_000)
    {
        ArgumentNullException.ThrowIfNull(innerGenerator);
        if (maxSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSize), maxSize, "Maximum cache size must be at least 1.");
        }

        _innerGenerator = innerGenerator;
        _maxSize = maxSize;
        _cache = new ConcurrentDictionary<string, Embedding<float>>();
        _lruQueue = new ConcurrentQueue<string>();
    }

    /// <inheritdoc/>
    public EmbeddingGeneratorMetadata Metadata => 
        _innerGenerator.GetService<EmbeddingGeneratorMetadata>() ?? 
        new EmbeddingGeneratorMetadata("Unknown");

    /// <inheritdoc/>
    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);

        var inputList = values as IList<string> ?? values.ToList();
        var results = new Embedding<float>[inputList.Count];
        var uncachedIndices = new List<int>();
        var uncachedTexts = new List<string>();

        for (int i = 0; i < inputList.Count; i++)
        {
            var text = inputList[i];
            var key = ComputeHash(text);

            if (_cache.TryGetValue(key, out var cachedEmbedding))
            {
                results[i] = cachedEmbedding;
            }
            else
            {
                uncachedIndices.Add(i);
                uncachedTexts.Add(text);
            }
        }

        if (uncachedTexts.Count > 0)
        {
            var generated = await _innerGenerator.GenerateAsync(uncachedTexts, options, cancellationToken).ConfigureAwait(false);

            for (int j = 0; j < uncachedIndices.Count; j++)
            {
                var index = uncachedIndices[j];
                var text = uncachedTexts[j];
                var embedding = generated[j];

                results[index] = embedding;
                AddToCache(text, embedding);
            }

            return new GeneratedEmbeddings<Embedding<float>>(results)
            {
                Usage = generated.Usage
            };
        }

        return new GeneratedEmbeddings<Embedding<float>>(results);
    }

    /// <inheritdoc/>
    public object? GetService(Type serviceType, object? serviceKey = null)
        => _innerGenerator.GetService(serviceType, serviceKey);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_innerGenerator is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _cache.Clear();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_innerGenerator is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else if (_innerGenerator is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _cache.Clear();
    }

    private void AddToCache(string text, Embedding<float> embedding)
    {
        var key = ComputeHash(text);

        if (_cache.TryAdd(key, embedding))
        {
            _lruQueue.Enqueue(key);

            if (_cache.Count > _maxSize)
            {
                EvictOldest();
            }
        }
    }

    private void EvictOldest()
    {
        lock (_evictionLock)
        {
            while (_cache.Count > _maxSize && _lruQueue.TryDequeue(out var oldestKey))
            {
                _cache.TryRemove(oldestKey, out _);
            }
        }
    }

    private static string ComputeHash(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }
}
