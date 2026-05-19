using System.ClientModel;
using ElBruno.LocalEmbeddings.Azure.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;

namespace ElBruno.LocalEmbeddings.Azure;

/// <summary>
/// Hybrid embedding generator that tries local embeddings first, then falls back to Azure OpenAI.
/// </summary>
/// <remarks>
/// This decorator wraps an <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> and adds
/// Azure OpenAI fallback capability. If the local generator fails, it automatically attempts
/// to generate embeddings using Azure OpenAI. This allows graceful degradation when local
/// generation encounters issues.
/// </remarks>
public sealed class HybridAzureEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>, IDisposable, IAsyncDisposable
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _localGenerator;
    private readonly OpenAIClient _azureClient;
    private readonly LocalEmbeddingsAzureOptions _options;
    private readonly ILogger<HybridAzureEmbeddingGenerator>? _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="HybridAzureEmbeddingGenerator"/> class.
    /// </summary>
    /// <param name="localGenerator">The local embedding generator to use as primary.</param>
    /// <param name="azureClient">The Azure OpenAI client for fallback.</param>
    /// <param name="options">Configuration options for fallback behavior.</param>
    /// <param name="logger">Optional logger for tracking fallback events.</param>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
    public HybridAzureEmbeddingGenerator(
        IEmbeddingGenerator<string, Embedding<float>> localGenerator,
        OpenAIClient azureClient,
        LocalEmbeddingsAzureOptions options,
        ILogger<HybridAzureEmbeddingGenerator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(localGenerator);
        ArgumentNullException.ThrowIfNull(azureClient);
        ArgumentNullException.ThrowIfNull(options);

        _localGenerator = localGenerator;
        _azureClient = azureClient;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    public EmbeddingGeneratorMetadata Metadata =>
        _localGenerator.GetService<EmbeddingGeneratorMetadata>() ??
        new EmbeddingGeneratorMetadata("HybridAzure");

    /// <inheritdoc/>
    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        ObjectDisposedException.ThrowIf(_disposed, GetType());

        var valuesList = values as IList<string> ?? values.ToList();

        try
        {
            _logger?.LogDebug("Attempting to generate embeddings locally for {Count} items.", valuesList.Count);
            var result = await _localGenerator.GenerateAsync(valuesList, options, cancellationToken).ConfigureAwait(false);
            _logger?.LogDebug("Successfully generated {Count} embeddings locally.", valuesList.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Local embedding generation failed, falling back to Azure OpenAI. Error: {Error}", ex.Message);
            return await FallbackToAzureAsync(valuesList, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public object? GetService(Type serviceType, object? serviceKey = null)
        => _localGenerator.GetService(serviceType, serviceKey);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_localGenerator is IDisposable disposable)
        {
            disposable.Dispose();
        }

        (_azureClient as IDisposable)?.Dispose();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_localGenerator is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else if (_localGenerator is IDisposable disposable)
        {
            disposable.Dispose();
        }

        if (_azureClient is IAsyncDisposable asyncDisposableClient)
        {
            await asyncDisposableClient.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            (_azureClient as IDisposable)?.Dispose();
        }
    }

    private async Task<GeneratedEmbeddings<Embedding<float>>> FallbackToAzureAsync(
        IEnumerable<string> values,
        CancellationToken cancellationToken)
    {
        var valuesList = values as IList<string> ?? values.ToList();
        var attempts = 0;
        Exception? lastException = null;

        while (attempts < _options.MaxFallbackAttempts)
        {
            attempts++;
            try
            {
                _logger?.LogInformation(
                    "Attempting Azure OpenAI fallback (attempt {Attempt}/{MaxAttempts})",
                    attempts,
                    _options.MaxFallbackAttempts);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(_options.TimeoutMilliseconds);

                var embeddingClient = _azureClient.GetEmbeddingClient(_options.DeploymentName);
                var embeddings = new Embedding<float>[valuesList.Count];

                for (int i = 0; i < valuesList.Count; i++)
                {
                    var response = await embeddingClient.GenerateEmbeddingAsync(
                        valuesList[i],
                        cancellationToken: cts.Token).ConfigureAwait(false);

                    // Extract embedding vector from the response
                    var embeddingData = response.Value;
                    // The OpenAI SDK returns an object with embedding data; convert it appropriately
                    embeddings[i] = new Embedding<float>(GetEmbeddingVector(embeddingData));
                }

                _logger?.LogInformation(
                    "Azure OpenAI fallback succeeded for {Count} embeddings.",
                    embeddings.Length);

                return new GeneratedEmbeddings<Embedding<float>>(embeddings);
            }
            catch (OperationCanceledException ex)
            {
                lastException = ex;
                _logger?.LogWarning(ex, "Azure OpenAI fallback timed out on attempt {Attempt}.", attempts);

                if (attempts < _options.MaxFallbackAttempts)
                {
                    await Task.Delay(1000 * attempts, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (ClientResultException ex)
            {
                lastException = ex;
                _logger?.LogWarning(
                    ex,
                    "Azure OpenAI fallback failed on attempt {Attempt}. Error: {Error}",
                    attempts,
                    ex.Message);

                if (attempts < _options.MaxFallbackAttempts)
                {
                    await Task.Delay(1000 * attempts, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger?.LogWarning(ex, "Azure OpenAI fallback encountered an error on attempt {Attempt}.", attempts);

                if (attempts < _options.MaxFallbackAttempts)
                {
                    await Task.Delay(1000 * attempts, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        _logger?.LogError(
            "All {MaxAttempts} Azure OpenAI fallback attempts failed. Last error: {Error}",
            _options.MaxFallbackAttempts,
            lastException?.Message ?? "Unknown");

        throw new InvalidOperationException(
            $"Failed to generate embeddings after {_options.MaxFallbackAttempts} attempts.",
            lastException);
    }

    private static float[] GetEmbeddingVector(object embeddingData)
    {
        // Use reflection to extract the embedding vector since the exact type depends on the OpenAI SDK version
        var embeddingProperty = embeddingData.GetType().GetProperty("Embedding")
            ?? embeddingData.GetType().GetProperty("Vector")
            ?? embeddingData.GetType().GetProperty("Data");

        if (embeddingProperty?.GetValue(embeddingData) is System.Collections.Generic.IEnumerable<float> embedding)
        {
            return embedding.ToArray();
        }

        // Fallback: try to use it directly if it's already an IEnumerable
        if (embeddingData is System.Collections.Generic.IEnumerable<float> directEmbedding)
        {
            return directEmbedding.ToArray();
        }

        throw new InvalidOperationException("Could not extract embedding vector from Azure OpenAI response.");
    }
}


