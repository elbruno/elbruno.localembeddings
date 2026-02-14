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
public sealed class LocalEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>, IAsyncDisposable
{
    private static readonly string[] QuantizedModelFileNames = ["model_quantized.onnx", "model_int8.onnx"];
    private static readonly HttpClient SharedModelDownloadHttpClient = new();
    private readonly OnnxEmbeddingModel _model;
    private readonly Tokenizer _tokenizer;
    private readonly EmbeddingGeneratorMetadata _metadata;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalEmbeddingGenerator"/> class with default options.
    /// </summary>
    /// <remarks>
    /// This is equivalent to <c>new LocalEmbeddingGenerator(new LocalEmbeddingsOptions())</c>.
    /// </remarks>
    public LocalEmbeddingGenerator()
        : this(new LocalEmbeddingsOptions())
    {
    }

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
    /// <para>
    /// <strong>Important:</strong> This constructor performs blocking initialization.
    /// In UI/ASP.NET request contexts, prefer <see cref="CreateAsync(CancellationToken)"/>
    /// or <see cref="CreateAsync(LocalEmbeddingsOptions, CancellationToken)"/>.
    /// </para>
    /// </remarks>
    public LocalEmbeddingGenerator(LocalEmbeddingsOptions options)
        : this(ResolveModelDirectory(options), options)
    {
    }

    private LocalEmbeddingGenerator(string modelDirectory, LocalEmbeddingsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Load the ONNX model
        _model = new OnnxEmbeddingModel();
        var modelPath = ResolveModelPath(modelDirectory, options.PreferQuantized);
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
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous creation operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when model download or loading fails.</exception>
    /// <remarks>
    /// <para>
    /// This factory method performs model download asynchronously and then initializes the generator.
    /// Use this in async contexts instead of the constructor to avoid sync-over-async blocking during download.
    /// </para>
    /// <para>
    /// The constructor remains available for backwards compatibility.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var generator = await LocalEmbeddingGenerator.CreateAsync();
    /// </code>
    /// </example>
    public static Task<LocalEmbeddingGenerator> CreateAsync(
        CancellationToken cancellationToken = default) =>
        CreateAsync(new LocalEmbeddingsOptions(), cancellationToken);

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
    /// This factory method performs model download asynchronously and then initializes the generator.
    /// Use this in async contexts instead of the constructor.
    /// </para>
    /// <para>
    /// The constructor remains available for backwards compatibility.
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
    public static async Task<LocalEmbeddingGenerator> CreateAsync(
        LocalEmbeddingsOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var modelDirectory = await ResolveModelDirectoryAsync(options, cancellationToken).ConfigureAwait(false);
        return new LocalEmbeddingGenerator(modelDirectory, options);
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
        var (inputIds, attentionMasks) = _tokenizer.TokenizeBatch(valuesList, maxLength: null, cancellationToken);

        // Generate embeddings in a single batched call
        var rawEmbeddings = _model.GenerateEmbeddings(inputIds, attentionMasks, cancellationToken);

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

    /// <summary>
    /// Asynchronously disposes the generator and releases underlying resources.
    /// </summary>
    /// <returns>A completed disposal task.</returns>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Counts tokens for the specified text using the model tokenizer.
    /// </summary>
    /// <param name="text">Text to tokenize and count.</param>
    /// <returns>The number of non-padding tokens produced by the tokenizer.</returns>
    public int CountTokens(string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _tokenizer.CountTokens(text);
    }

    private static string ResolveModelDirectory(LocalEmbeddingsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!string.IsNullOrWhiteSpace(options.ModelPath))
        {
            return options.ModelPath;
        }

        if (!options.EnsureModelDownloaded)
        {
            throw new InvalidOperationException(
                "Either ModelPath must be specified or EnsureModelDownloaded must be true.");
        }

        var downloader = new ModelDownloader(SharedModelDownloadHttpClient, options.CacheDirectory);
        return downloader.EnsureModelAsync(options.ModelName, options.PreferQuantized).GetAwaiter().GetResult();
    }

    private static async Task<string> ResolveModelDirectoryAsync(LocalEmbeddingsOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!string.IsNullOrWhiteSpace(options.ModelPath))
        {
            return options.ModelPath;
        }

        if (!options.EnsureModelDownloaded)
        {
            throw new InvalidOperationException(
                "Either ModelPath must be specified or EnsureModelDownloaded must be true.");
        }

        var downloader = new ModelDownloader(SharedModelDownloadHttpClient, options.CacheDirectory);
        return await downloader.EnsureModelAsync(options.ModelName, options.PreferQuantized, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static string ResolveModelPath(string modelDirectory, bool preferQuantized)
    {
        if (preferQuantized)
        {
            foreach (var quantizedModelFileName in QuantizedModelFileNames)
            {
                var quantizedModelPath = Path.Combine(modelDirectory, quantizedModelFileName);
                if (File.Exists(quantizedModelPath))
                {
                    return quantizedModelPath;
                }
            }
        }

        return Path.Combine(modelDirectory, "model.onnx");
    }
}
