using ElBruno.LocalEmbeddings.Options;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalEmbeddings;

/// <summary>
/// Generates embeddings locally using ONNX Runtime models.
/// </summary>
/// <remarks>
/// <para>
/// This class implements <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> for local
/// embedding generation using ONNX Runtime. It downloads and caches HuggingFace models
/// automatically on first use (unless configured otherwise).
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> This class is thread-safe after construction. Multiple
/// threads can call <see cref="GenerateAsync"/> concurrently. The underlying ONNX Runtime
/// session and tokenizer are designed for concurrent access.
/// </para>
/// </remarks>
public sealed class LocalEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly OnnxEmbeddingModel _model;
    private readonly Tokenizer _tokenizer;
    private readonly EmbeddingGeneratorMetadata _metadata;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalEmbeddingGenerator"/> class.
    /// </summary>
    /// <param name="options">The configuration options for embedding generation.</param>
    /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when model download or loading fails.</exception>
    /// <remarks>
    /// If <see cref="LocalEmbeddingsOptions.EnsureModelDownloaded"/> is true and 
    /// <see cref="LocalEmbeddingsOptions.ModelPath"/> is not specified, the model
    /// will be downloaded synchronously during construction.
    /// </remarks>
    public LocalEmbeddingGenerator(LocalEmbeddingsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string modelDirectory;

        if (!string.IsNullOrWhiteSpace(options.ModelPath))
        {
            // Use the provided local model path
            modelDirectory = options.ModelPath;
        }
        else if (options.EnsureModelDownloaded)
        {
            // Download the model synchronously
            var downloader = new ModelDownloader(new HttpClient(), options.CacheDirectory);
            modelDirectory = downloader.EnsureModelAsync(options.ModelName).GetAwaiter().GetResult();
        }
        else
        {
            throw new InvalidOperationException(
                "Either ModelPath must be specified or EnsureModelDownloaded must be true.");
        }

        // Load the ONNX model
        _model = new OnnxEmbeddingModel();
        var modelPath = Path.Combine(modelDirectory, "model.onnx");
        _model.Load(
            modelPath,
            options.NormalizeEmbeddings,
            options.UseParallelExecution,
            options.InterOpNumThreads,
            options.IntraOpNumThreads);

        // Initialize the tokenizer
        _tokenizer = new Tokenizer(modelDirectory, options.MaxSequenceLength);

        // Create metadata
        _metadata = new EmbeddingGeneratorMetadata(
            providerName: "LocalEmbeddings",
            providerUri: new Uri("https://github.com/elbruno/elbruno.localembeddings"),
            defaultModelId: options.ModelName,
            defaultModelDimensions: _model.EmbeddingDimension);
    }

    /// <inheritdoc />
    public EmbeddingGeneratorMetadata Metadata => _metadata;

    /// <summary>
    /// Creates a new instance of <see cref="LocalEmbeddingGenerator"/> asynchronously.
    /// </summary>
    /// <param name="options">The configuration options for embedding generation.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous creation operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when model download or loading fails.</exception>
    /// <remarks>
    /// <para>
    /// This factory method performs model download and ONNX session creation on a background thread,
    /// avoiding blocking the caller's thread. Use this in async contexts instead of the constructor.
    /// </para>
    /// <para>
    /// This is equivalent to calling the constructor but wrapped in <see cref="Task.Run{TResult}(Func{TResult})"/>
    /// for non-blocking initialization.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var generator = await LocalEmbeddingGenerator.CreateAsync(new LocalEmbeddingsOptions
    /// {
    ///     ModelName = "sentence-transformers/all-MiniLM-L6-v2"
    /// });
    /// </code>
    /// </example>
    public static Task<LocalEmbeddingGenerator> CreateAsync(
        LocalEmbeddingsOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        return Task.Run(() => new LocalEmbeddingGenerator(options), cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// This method is thread-safe and can be called concurrently from multiple threads.
    /// </remarks>
    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(values);

        var valuesList = values.ToList();
        if (valuesList.Count == 0)
        {
            return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>());
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Tokenize all inputs
        var (inputIds, attentionMasks) = _tokenizer.TokenizeBatch(valuesList);

        // Generate embeddings in a single batched call
        var rawEmbeddings = _model.GenerateEmbeddings(inputIds, attentionMasks);

        // Wrap results in the M.E.AI types
        var result = new GeneratedEmbeddings<Embedding<float>>(
            rawEmbeddings.Select(e => new Embedding<float>(e)).ToList());

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

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;

        _model.Dispose();
        _disposed = true;
    }
}
