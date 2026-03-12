using ElBruno.LocalEmbeddings.Npu.Options;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalEmbeddings.Npu;

/// <summary>
/// Generates embeddings locally using ONNX Runtime with DirectML NPU acceleration.
/// </summary>
/// <remarks>
/// <para>
/// This class implements <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> for local
/// embedding generation using ONNX Runtime with the DirectML execution provider.
/// It leverages NPU or GPU hardware for accelerated inference.
/// </para>
/// <para>
/// Models are downloaded and cached from HuggingFace automatically on first use.
/// By default, quantized (INT8) models are preferred for optimal NPU performance.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> This class is thread-safe after construction.
/// </para>
/// </remarks>
public sealed class NpuEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>, IAsyncDisposable
{
    private static readonly string[] QuantizedModelFileNames = ["model_quantized.onnx", "model_int8.onnx"];
    private static readonly HttpClient SharedModelDownloadHttpClient = new();
    private readonly NpuOnnxEmbeddingModel _model;
    private readonly Tokenizer _tokenizer;
    private readonly EmbeddingGeneratorMetadata _metadata;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="NpuEmbeddingGenerator"/> class with default options.
    /// </summary>
    public NpuEmbeddingGenerator()
        : this(new NpuEmbeddingsOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NpuEmbeddingGenerator"/> class.
    /// </summary>
    /// <param name="options">The configuration options for NPU embedding generation.</param>
    /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when model download or loading fails.</exception>
    public NpuEmbeddingGenerator(NpuEmbeddingsOptions options)
        : this(ResolveModelDirectory(options), options)
    {
    }

    private NpuEmbeddingGenerator(string modelDirectory, NpuEmbeddingsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _model = new NpuOnnxEmbeddingModel();
        var modelPath = ResolveModelPath(modelDirectory, options.PreferQuantized);
        _model.Load(
            modelPath,
            options.NormalizeEmbeddings,
            options.DeviceId,
            options.AutoDetectNpu);

        _tokenizer = new Tokenizer(modelDirectory, options.MaxSequenceLength);

        _metadata = new EmbeddingGeneratorMetadata(
            providerName: "LocalEmbeddings.Npu",
            providerUri: new Uri("https://github.com/elbruno/elbruno.localembeddings"),
            defaultModelId: options.ModelName,
            defaultModelDimensions: _model.EmbeddingDimension);
    }

    /// <inheritdoc />
    public EmbeddingGeneratorMetadata Metadata => _metadata;

    /// <summary>
    /// Gets a value indicating whether DirectML is targeting an NPU device.
    /// </summary>
    public bool IsNpuActive => _model.IsNpuActive;

    /// <summary>
    /// Gets the reason why NPU was not selected, or <c>null</c> if NPU is active.
    /// </summary>
    public string? FallbackReason => _model.FallbackReason;

    /// <summary>
    /// Gets the description of the selected DirectML device.
    /// </summary>
    public string? DeviceDescription => _model.DeviceDescription;

    /// <summary>
    /// Gets the DirectML device ID used for inference.
    /// </summary>
    public int ActiveDeviceId => _model.ActiveDeviceId;

    /// <summary>
    /// Creates a new instance of <see cref="NpuEmbeddingGenerator"/> asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous creation operation.</returns>
    public static Task<NpuEmbeddingGenerator> CreateAsync(
        CancellationToken cancellationToken = default) =>
        CreateAsync(new NpuEmbeddingsOptions(), null, cancellationToken);

    /// <summary>
    /// Creates a new instance of <see cref="NpuEmbeddingGenerator"/> asynchronously with progress reporting.
    /// </summary>
    /// <param name="options">The configuration options for NPU embedding generation.</param>
    /// <param name="progress">An optional progress reporter that receives download progress (0.0 to 1.0).</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous creation operation.</returns>
    public static Task<NpuEmbeddingGenerator> CreateAsync(
        NpuEmbeddingsOptions options,
        IProgress<double>? progress,
        CancellationToken cancellationToken = default) =>
        CreateAsyncCore(options, progress, cancellationToken);

    /// <summary>
    /// Creates a new instance of <see cref="NpuEmbeddingGenerator"/> asynchronously.
    /// </summary>
    /// <param name="options">The configuration options for NPU embedding generation.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous creation operation.</returns>
    public static Task<NpuEmbeddingGenerator> CreateAsync(
        NpuEmbeddingsOptions options,
        CancellationToken cancellationToken = default) =>
        CreateAsyncCore(options, null, cancellationToken);

    private static async Task<NpuEmbeddingGenerator> CreateAsyncCore(
        NpuEmbeddingsOptions options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        var modelDirectory = await ResolveModelDirectoryAsync(options, progress, cancellationToken).ConfigureAwait(false);
        return new NpuEmbeddingGenerator(modelDirectory, options);
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

        var (inputIds, attentionMasks) = _tokenizer.TokenizeBatch(valuesList, maxLength: null, cancellationToken);
        var rawEmbeddings = _model.GenerateEmbeddings(inputIds, attentionMasks, cancellationToken);

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

    private static string ResolveModelDirectory(NpuEmbeddingsOptions options)
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
        return downloader.EnsureModelAsync(options.ModelName, options.PreferQuantized, null, options.ExpectedHash).GetAwaiter().GetResult();
    }

    private static async Task<string> ResolveModelDirectoryAsync(NpuEmbeddingsOptions options, IProgress<double>? progress, CancellationToken cancellationToken)
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
        return await downloader.EnsureModelAsync(options.ModelName, options.PreferQuantized, progress, options.ExpectedHash, cancellationToken).ConfigureAwait(false);
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
