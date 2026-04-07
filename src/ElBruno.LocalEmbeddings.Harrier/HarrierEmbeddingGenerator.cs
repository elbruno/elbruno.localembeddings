using ElBruno.LocalEmbeddings.Harrier.Options;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalEmbeddings.Harrier;

/// <summary>
/// Generates embeddings locally using the Harrier-OSS-v1 ONNX model.
/// </summary>
/// <remarks>
/// <para>
/// This class implements <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> for local
/// embedding generation using the Microsoft Harrier model via ONNX Runtime.
/// It downloads and caches the model from HuggingFace automatically on first use.
/// </para>
/// <para>
/// The Harrier model is a decoder-only transformer (Gemma 3 based) that produces
/// 640-dimensional embeddings with built-in last-token pooling and L2 normalization.
/// It supports 94+ languages and up to 32,768 token context windows.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> This class is thread-safe after construction.
/// </para>
/// </remarks>
public sealed class HarrierEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>, IAsyncDisposable
{
    private static readonly HttpClient SharedModelDownloadHttpClient = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2)
    });
    private readonly HarrierOnnxEmbeddingModel _model;
    private readonly HarrierTokenizer _tokenizer;
    private readonly EmbeddingGeneratorMetadata _metadata;
    private bool _disposed;

    private HarrierEmbeddingGenerator(string modelDirectory, HarrierEmbeddingsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Load the ONNX model
        _model = new HarrierOnnxEmbeddingModel();
        var modelPath = HarrierModelDownloader.ResolveModelPath(modelDirectory, options.ModelVariant);
        _model.Load(
            modelPath,
            options.UseParallelExecution,
            options.InterOpNumThreads,
            options.IntraOpNumThreads);

        // Initialize the tokenizer with instruction prefix
        _tokenizer = HarrierTokenizer.Create(
            modelDirectory,
            options.MaxSequenceLength,
            options.InstructionPrefix);

        // Create metadata
        _metadata = new EmbeddingGeneratorMetadata(
            providerName: "ElBruno.LocalEmbeddings.Harrier",
            providerUri: new Uri("https://github.com/elbruno/elbruno.localembeddings"),
            defaultModelId: options.ModelName,
            defaultModelDimensions: _model.EmbeddingDimension);
    }

    /// <inheritdoc />
    public EmbeddingGeneratorMetadata Metadata => _metadata;

    /// <summary>
    /// Creates a new instance of <see cref="HarrierEmbeddingGenerator"/> with default options.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous creation operation.</returns>
    public static Task<HarrierEmbeddingGenerator> CreateAsync(
        CancellationToken cancellationToken = default)
        => CreateAsync(new HarrierEmbeddingsOptions(), null, cancellationToken);

    /// <summary>
    /// Creates a new instance of <see cref="HarrierEmbeddingGenerator"/> asynchronously.
    /// </summary>
    /// <param name="options">The configuration options.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous creation operation.</returns>
    /// <remarks>
    /// This factory method downloads the model asynchronously before initializing the generator.
    /// Use this in async contexts to avoid blocking.
    /// </remarks>
    public static Task<HarrierEmbeddingGenerator> CreateAsync(
        HarrierEmbeddingsOptions options,
        CancellationToken cancellationToken = default)
        => CreateAsync(options, null, cancellationToken);

    /// <summary>
    /// Creates a new instance of <see cref="HarrierEmbeddingGenerator"/> asynchronously with progress reporting.
    /// </summary>
    /// <param name="options">The configuration options.</param>
    /// <param name="progress">Optional progress reporter (0.0 to 1.0).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task representing the asynchronous creation operation.</returns>
    public static async Task<HarrierEmbeddingGenerator> CreateAsync(
        HarrierEmbeddingsOptions options,
        IProgress<double>? progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var modelDirectory = await ResolveModelDirectoryAsync(options, progress, cancellationToken).ConfigureAwait(false);
        return new HarrierEmbeddingGenerator(modelDirectory, options);
    }

    /// <inheritdoc />
    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(values);

        IList<string> valuesList = values as IList<string> ?? values.ToList();
        if (valuesList.Count == 0)
        {
            return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>());
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Tokenize all inputs (instruction prefix is applied by the tokenizer)
        var (inputIds, attentionMasks) = _tokenizer.TokenizeBatch(valuesList, maxLength: null, cancellationToken);

        // Generate embeddings in a single batched call
        var rawEmbeddings = _model.GenerateEmbeddings(inputIds, attentionMasks, cancellationToken);

        // Wrap results in M.E.AI types
        var result = new GeneratedEmbeddings<Embedding<float>>(
            rawEmbeddings.Select(e => new Embedding<float>(e)));

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public TService? GetService<TService>(object? key = null) where TService : class
    {
        if (typeof(TService) == typeof(EmbeddingGeneratorMetadata))
            return Metadata as TService;

        return typeof(TService) == typeof(IEmbeddingGenerator<string, Embedding<float>>)
            ? (TService)(object)this
            : null;
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? key = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        if (serviceType == typeof(EmbeddingGeneratorMetadata))
            return Metadata;

        return serviceType == typeof(IEmbeddingGenerator<string, Embedding<float>>)
            ? this
            : null;
    }

    /// <summary>
    /// Counts tokens for the specified text using the Harrier tokenizer.
    /// </summary>
    /// <param name="text">Text to tokenize and count.</param>
    /// <returns>The number of non-padding tokens (including BOS, EOS, and instruction prefix).</returns>
    public int CountTokens(string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _tokenizer.CountTokens(text);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;

        _model.Dispose();
        _disposed = true;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private static async Task<string> ResolveModelDirectoryAsync(
        HarrierEmbeddingsOptions options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(options.ModelPath))
        {
            return options.ModelPath;
        }

        if (!options.EnsureModelDownloaded)
        {
            throw new InvalidOperationException(
                "Either ModelPath must be specified or EnsureModelDownloaded must be true.");
        }

        var downloader = new HarrierModelDownloader(SharedModelDownloadHttpClient, options);
        return await downloader.EnsureModelAsync(progress, cancellationToken).ConfigureAwait(false);
    }
}
