using ElBruno.LocalEmbeddings.Npu.Qualcomm.Options;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalEmbeddings.Npu.Qualcomm;

/// <summary>
/// Generates embeddings locally using ONNX Runtime with Qualcomm QNN NPU acceleration.
/// </summary>
/// <remarks>
/// <para>
/// This class implements <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> for local
/// embedding generation using the Qualcomm QNN execution provider targeting the
/// Hexagon Tensor Processor (HTP) NPU on Snapdragon X series processors.
/// </para>
/// <para>
/// If QNN is not available (e.g., running on non-Qualcomm hardware), it falls back
/// to CPU execution automatically.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> This class is thread-safe after construction.
/// </para>
/// </remarks>
public sealed class QualcommEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>, IAsyncDisposable
{
    private static readonly string[] QuantizedModelFileNames = ["model_quantized.onnx", "model_int8.onnx"];
    private static readonly HttpClient SharedModelDownloadHttpClient = new();
    private readonly QualcommOnnxEmbeddingModel _model;
    private readonly Tokenizer _tokenizer;
    private readonly EmbeddingGeneratorMetadata _metadata;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="QualcommEmbeddingGenerator"/> class with default options.
    /// </summary>
    public QualcommEmbeddingGenerator()
        : this(new QualcommEmbeddingsOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QualcommEmbeddingGenerator"/> class.
    /// </summary>
    /// <param name="options">The configuration options for Qualcomm NPU embedding generation.</param>
    public QualcommEmbeddingGenerator(QualcommEmbeddingsOptions options)
        : this(ResolveModelDirectory(options), options)
    {
    }

    private QualcommEmbeddingGenerator(string modelDirectory, QualcommEmbeddingsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _model = new QualcommOnnxEmbeddingModel();
        var modelPath = ResolveModelPath(modelDirectory, options.PreferQuantized);
        _model.Load(
            modelPath,
            options.NormalizeEmbeddings,
            options.QnnBackendPath,
            options.FallbackToCpu);

        _tokenizer = new Tokenizer(modelDirectory, options.MaxSequenceLength);

        _metadata = new EmbeddingGeneratorMetadata(
            providerName: "LocalEmbeddings.Npu.Qualcomm",
            providerUri: new Uri("https://github.com/elbruno/elbruno.localembeddings"),
            defaultModelId: options.ModelName,
            defaultModelDimensions: _model.EmbeddingDimension);
    }

    /// <inheritdoc />
    public EmbeddingGeneratorMetadata Metadata => _metadata;

    /// <summary>
    /// Gets a value indicating whether the QNN execution provider is active.
    /// When false, the generator is running on CPU as a fallback.
    /// </summary>
    public bool IsQnnActive => _model.IsQnnActive;

    /// <summary>
    /// Gets the reason why the QNN execution provider could not be loaded,
    /// or <c>null</c> if QNN is active.
    /// </summary>
    public string? FallbackReason => _model.FallbackReason;

    /// <summary>
    /// Creates a new instance of <see cref="QualcommEmbeddingGenerator"/> asynchronously.
    /// </summary>
    public static Task<QualcommEmbeddingGenerator> CreateAsync(
        CancellationToken cancellationToken = default) =>
        CreateAsync(new QualcommEmbeddingsOptions(), null, cancellationToken);

    /// <summary>
    /// Creates a new instance of <see cref="QualcommEmbeddingGenerator"/> asynchronously with progress reporting.
    /// </summary>
    public static Task<QualcommEmbeddingGenerator> CreateAsync(
        QualcommEmbeddingsOptions options,
        IProgress<double>? progress,
        CancellationToken cancellationToken = default) =>
        CreateAsyncCore(options, progress, cancellationToken);

    /// <summary>
    /// Creates a new instance of <see cref="QualcommEmbeddingGenerator"/> asynchronously.
    /// </summary>
    public static Task<QualcommEmbeddingGenerator> CreateAsync(
        QualcommEmbeddingsOptions options,
        CancellationToken cancellationToken = default) =>
        CreateAsyncCore(options, null, cancellationToken);

    private static async Task<QualcommEmbeddingGenerator> CreateAsyncCore(
        QualcommEmbeddingsOptions options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        var modelDirectory = await ResolveModelDirectoryAsync(options, progress, cancellationToken).ConfigureAwait(false);
        return new QualcommEmbeddingGenerator(modelDirectory, options);
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
    public int CountTokens(string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _tokenizer.CountTokens(text);
    }

    private static string ResolveModelDirectory(QualcommEmbeddingsOptions options)
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

    private static async Task<string> ResolveModelDirectoryAsync(QualcommEmbeddingsOptions options, IProgress<double>? progress, CancellationToken cancellationToken)
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
