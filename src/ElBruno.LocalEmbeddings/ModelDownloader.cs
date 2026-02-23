using ElBruno.HuggingFace;

namespace ElBruno.LocalEmbeddings;

/// <summary>
/// Interface for downloading and caching ONNX models.
/// </summary>
public interface IModelDownloader
{
    /// <summary>
    /// Downloads a model if not already cached.
    /// </summary>
    /// <param name="modelName">The HuggingFace model name (e.g., "sentence-transformers/all-MiniLM-L6-v2").</param>
    /// <param name="preferQuantized">Whether to prefer quantized model files when available.</param>
    /// <param name="progress">Optional progress reporter (0.0 to 1.0).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The local path to the model directory.</returns>
    Task<string> EnsureModelAsync(string modelName, bool preferQuantized = false, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the local cache directory for models.
    /// </summary>
    /// <returns>The cache directory path.</returns>
    string GetCacheDirectory();
}

/// <summary>
/// Downloads and caches ONNX models from HuggingFace Hub.
/// </summary>
public sealed class ModelDownloader : IModelDownloader
{
    private const string DefaultModel = "sentence-transformers/all-MiniLM-L6-v2";
    private static readonly string[] QuantizedModelFiles = ["model_quantized.onnx", "model_int8.onnx"];
    private static readonly string[] TokenizerFiles = ["tokenizer.json", "tokenizer_config.json", "vocab.txt"];

    private readonly HuggingFaceDownloader _downloader;
    private readonly string _cacheDirectory;

    /// <summary>
    /// Creates a new ModelDownloader with default settings.
    /// </summary>
    public ModelDownloader() : this(new HttpClient(), null)
    {
    }

    /// <summary>
    /// Creates a new ModelDownloader with a custom HttpClient.
    /// </summary>
    /// <param name="httpClient">The HttpClient to use for downloads.</param>
    /// <param name="cacheDirectory">Optional custom cache directory.</param>
    public ModelDownloader(HttpClient httpClient, string? cacheDirectory = null)
    {
        if (httpClient == null)
        {
            throw new ArgumentNullException(nameof(httpClient));
        }

        _downloader = new HuggingFaceDownloader(httpClient);
        _cacheDirectory = cacheDirectory ?? DefaultPathHelper.GetDefaultCacheDirectory("LocalEmbeddings");
    }

    /// <summary>
    /// Gets the default HuggingFace model name.
    /// </summary>
    public static string DefaultModelName => DefaultModel;

    /// <inheritdoc />
    public async Task<string> EnsureModelAsync(string modelName, bool preferQuantized = false, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            throw new ArgumentException("Model name cannot be null or empty.", nameof(modelName));
        }

        var sanitizedName = DefaultPathHelper.SanitizeModelName(modelName);
        var modelDirectory = Path.Combine(_cacheDirectory, sanitizedName);

        // Build the list of required and optional files
        var requiredFiles = new List<string>();
        var optionalFiles = new List<string>(TokenizerFiles.Select(f => f));

        // Determine which ONNX model file to download
        // Check if any model already exists locally
        var existingDefaultModel = File.Exists(Path.Combine(modelDirectory, "model.onnx"));
        var existingQuantizedModel = QuantizedModelFiles
            .FirstOrDefault(f => File.Exists(Path.Combine(modelDirectory, f)));

        if (existingDefaultModel || existingQuantizedModel != null)
        {
            // Files already exist, no need to download
            // Still check for tokenizer files
            if (preferQuantized && existingQuantizedModel != null)
            {
                // Use existing quantized model
            }
            else if (existingDefaultModel)
            {
                // Use existing default model
            }
        }
        else
        {
            // Need to download - decide what to request
            if (preferQuantized)
            {
                // Try quantized files as optional, fall back to default model.onnx as required
                foreach (var quantizedFile in QuantizedModelFiles)
                {
                    optionalFiles.Insert(0, $"onnx/{quantizedFile}");
                }
                requiredFiles.Add("onnx/model.onnx");
            }
            else
            {
                // Just download the default model
                requiredFiles.Add("onnx/model.onnx");
            }
        }

        // Map progress from DownloadProgress to double
        IProgress<DownloadProgress>? downloadProgress = null;
        if (progress != null)
        {
            downloadProgress = new Progress<DownloadProgress>(p =>
            {
                progress.Report(p.PercentComplete / 100.0);
            });
        }

        // Download files if needed
        await _downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = modelName,
            LocalDirectory = modelDirectory,
            RequiredFiles = requiredFiles,
            OptionalFiles = optionalFiles,
            Progress = downloadProgress
        }, cancellationToken).ConfigureAwait(false);

        // Verify at least one ONNX model file exists
        var finalDefaultModel = File.Exists(Path.Combine(modelDirectory, "model.onnx"));
        var finalQuantizedModel = QuantizedModelFiles
            .FirstOrDefault(f => File.Exists(Path.Combine(modelDirectory, f)));

        if (!finalDefaultModel && finalQuantizedModel == null)
        {
            throw new InvalidOperationException($"Model file was not downloaded successfully in {modelDirectory}");
        }

        return modelDirectory;
    }

    /// <inheritdoc />
    public string GetCacheDirectory() => _cacheDirectory;
}
