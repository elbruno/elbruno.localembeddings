using ElBruno.LocalEmbeddings.Npu.Intel.Options;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalEmbeddings.Npu.Intel;

/// <summary>
/// Generates embeddings locally using ONNX Runtime with Intel OpenVINO NPU acceleration.
/// </summary>
/// <remarks>
/// <para>
/// This class implements <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> for local
/// embedding generation using the Intel OpenVINO execution provider targeting the
/// Intel AI Boost NPU found in Intel Core Ultra processors.
/// </para>
/// <para>
/// If OpenVINO is not available (e.g., not installed or running on non-Intel hardware),
/// it falls back to CPU execution automatically.
/// </para>
/// <para>
/// <strong>Prerequisites:</strong> Intel OpenVINO runtime must be installed.
/// Run <c>setupvars.bat</c> (Windows) before launching your application.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> This class is thread-safe after construction.
/// </para>
/// </remarks>
public sealed class IntelEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>, IAsyncDisposable
{
    private static readonly string[] QuantizedModelFileNames = ["model_quantized.onnx", "model_int8.onnx"];
    private static readonly HttpClient SharedModelDownloadHttpClient = new();
    private readonly IntelOnnxEmbeddingModel _model;
    private readonly IntelTokenizer _tokenizer;
    private readonly EmbeddingGeneratorMetadata _metadata;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntelEmbeddingGenerator"/> class with default options.
    /// </summary>
    public IntelEmbeddingGenerator()
        : this(new IntelEmbeddingsOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IntelEmbeddingGenerator"/> class.
    /// </summary>
    /// <param name="options">The configuration options for Intel NPU embedding generation.</param>
    public IntelEmbeddingGenerator(IntelEmbeddingsOptions options)
        : this(ResolveModelDirectory(options), options)
    {
    }

    private IntelEmbeddingGenerator(string modelDirectory, IntelEmbeddingsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _model = new IntelOnnxEmbeddingModel();
        var modelPath = ResolveModelPath(modelDirectory, options.PreferQuantized);
        _model.Load(
            modelPath,
            options.NormalizeEmbeddings,
            options.DeviceType,
            options.FallbackToCpu);

        _tokenizer = new IntelTokenizer(modelDirectory, options.MaxSequenceLength);

        _metadata = new EmbeddingGeneratorMetadata(
            providerName: "LocalEmbeddings.Npu.Intel",
            providerUri: new Uri("https://github.com/elbruno/elbruno.localembeddings"),
            defaultModelId: options.ModelName,
            defaultModelDimensions: _model.EmbeddingDimension);
    }

    /// <inheritdoc />
    public EmbeddingGeneratorMetadata Metadata => _metadata;

    /// <summary>
    /// Gets a value indicating whether the OpenVINO execution provider is active.
    /// When false, the generator is running on CPU as a fallback.
    /// </summary>
    public bool IsOpenVinoActive => _model.IsOpenVinoActive;

    /// <summary>
    /// Creates a new instance of <see cref="IntelEmbeddingGenerator"/> asynchronously.
    /// </summary>
    public static Task<IntelEmbeddingGenerator> CreateAsync(
        CancellationToken cancellationToken = default) =>
        CreateAsync(new IntelEmbeddingsOptions(), null, cancellationToken);

    /// <summary>
    /// Creates a new instance of <see cref="IntelEmbeddingGenerator"/> asynchronously with progress reporting.
    /// </summary>
    public static Task<IntelEmbeddingGenerator> CreateAsync(
        IntelEmbeddingsOptions options,
        IProgress<double>? progress,
        CancellationToken cancellationToken = default) =>
        CreateAsyncCore(options, progress, cancellationToken);

    /// <summary>
    /// Creates a new instance of <see cref="IntelEmbeddingGenerator"/> asynchronously.
    /// </summary>
    public static Task<IntelEmbeddingGenerator> CreateAsync(
        IntelEmbeddingsOptions options,
        CancellationToken cancellationToken = default) =>
        CreateAsyncCore(options, null, cancellationToken);

    private static async Task<IntelEmbeddingGenerator> CreateAsyncCore(
        IntelEmbeddingsOptions options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        var modelDirectory = await ResolveModelDirectoryAsync(options, progress, cancellationToken).ConfigureAwait(false);
        return new IntelEmbeddingGenerator(modelDirectory, options);
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

    private static string ResolveModelDirectory(IntelEmbeddingsOptions options)
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

        var downloader = new IntelModelDownloader(SharedModelDownloadHttpClient, options.CacheDirectory);
        return downloader.EnsureModelAsync(options.ModelName, options.PreferQuantized, null, options.ExpectedHash).GetAwaiter().GetResult();
    }

    private static async Task<string> ResolveModelDirectoryAsync(IntelEmbeddingsOptions options, IProgress<double>? progress, CancellationToken cancellationToken)
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

        var downloader = new IntelModelDownloader(SharedModelDownloadHttpClient, options.CacheDirectory);
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
