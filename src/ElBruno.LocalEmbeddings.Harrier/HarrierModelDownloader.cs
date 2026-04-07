using System.Collections.Concurrent;
using System.Security.Cryptography;
using ElBruno.HuggingFace;
using ElBruno.LocalEmbeddings.Harrier.Options;

namespace ElBruno.LocalEmbeddings.Harrier;

/// <summary>
/// Downloads and caches Harrier ONNX models from HuggingFace Hub using
/// <see cref="HuggingFaceDownloader"/> from the <c>ElBruno.HuggingFace.Downloader</c> package.
/// </summary>
/// <remarks>
/// Handles Harrier-specific download needs:
/// <list type="bullet">
///   <item>External weight files (<c>.onnx_data</c>) alongside the ONNX model</item>
///   <item>Variant-specific file paths (fp32, fp16, quantized, q4)</item>
///   <item><c>tokenizer.json</c> instead of <c>vocab.txt</c></item>
/// </list>
/// </remarks>
public sealed class HarrierModelDownloader
{
    private static readonly string[] TokenizerFiles = ["tokenizer.json", "tokenizer_config.json"];
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _downloadLocks = new(StringComparer.OrdinalIgnoreCase);

    private readonly HuggingFaceDownloader _downloader;
    private readonly HarrierEmbeddingsOptions _options;
    private readonly string _cacheDirectory;

    /// <summary>
    /// Creates a new HarrierModelDownloader.
    /// </summary>
    /// <param name="httpClient">The HttpClient to use for downloads.</param>
    /// <param name="options">The Harrier embedding options.</param>
    public HarrierModelDownloader(HttpClient httpClient, HarrierEmbeddingsOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
        _downloader = new HuggingFaceDownloader(httpClient);
        _cacheDirectory = options.CacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ElBruno", "LocalEmbeddings", "models");
    }

    /// <summary>
    /// Creates a new HarrierModelDownloader with default HttpClient settings.
    /// </summary>
    /// <param name="options">The Harrier embedding options.</param>
    public HarrierModelDownloader(HarrierEmbeddingsOptions options)
        : this(new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) }), options)
    {
    }

    /// <summary>
    /// Ensures the Harrier model files are downloaded and cached.
    /// </summary>
    /// <param name="progress">Optional progress reporter (0.0 to 1.0).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The local path to the model directory.</returns>
    public async Task<string> EnsureModelAsync(
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sanitizedName = DefaultPathHelper.SanitizeModelName(_options.ModelName);
        var modelDirectory = Path.GetFullPath(Path.Combine(_cacheDirectory, sanitizedName));
        var cacheRoot = Path.GetFullPath(_cacheDirectory);
        if (!modelDirectory.StartsWith(cacheRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Model name '{_options.ModelName}' resolves to a path outside the cache directory.");
        }

        // Determine which files to download based on the variant
        var onnxFileName = GetOnnxFileName(_options.ModelVariant);
        var onnxDataFileName = onnxFileName + "_data";

        var downloadLock = _downloadLocks.GetOrAdd(modelDirectory, _ => new SemaphoreSlim(1, 1));
        await downloadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Check if model already exists
            var modelPath = Path.Combine(modelDirectory, onnxFileName);
            var tokenizerPath = Path.Combine(modelDirectory, "tokenizer.json");
            var onnxDataPath = Path.Combine(modelDirectory, onnxDataFileName);
            if (File.Exists(modelPath) && File.Exists(tokenizerPath) && File.Exists(onnxDataPath))
            {
                if (SidecarHashValid(modelPath) && SidecarHashValid(onnxDataPath))
                {
                    return modelDirectory;
                }

                DeleteIfExists(modelPath);
                DeleteIfExists(modelPath + ".sha256");
                DeleteIfExists(onnxDataPath);
                DeleteIfExists(onnxDataPath + ".sha256");
            }

            // Build file lists for download
            var requiredFiles = new List<string>
            {
                $"onnx/{onnxFileName}",
                $"onnx/{onnxDataFileName}"
            };
            var optionalFiles = new List<string>(TokenizerFiles);

            // Map progress
            IProgress<DownloadProgress>? downloadProgress = null;
            if (progress != null)
            {
                downloadProgress = new Progress<DownloadProgress>(p =>
                {
                    progress.Report(p.PercentComplete / 100.0);
                });
            }

            await _downloader.DownloadFilesAsync(new DownloadRequest
            {
                RepoId = _options.ModelName,
                LocalDirectory = modelDirectory,
                RequiredFiles = requiredFiles,
                OptionalFiles = optionalFiles,
                Progress = downloadProgress
            }, cancellationToken).ConfigureAwait(false);

            // Move files from onnx/ subdirectory to model directory root
            var onnxSubDir = Path.Combine(modelDirectory, "onnx");
            if (Directory.Exists(onnxSubDir))
            {
                var filesToMove = new[] { onnxFileName, onnxDataFileName };
                foreach (var fileName in filesToMove)
                {
                    var sourcePath = Path.Combine(onnxSubDir, fileName);
                    var destPath = Path.Combine(modelDirectory, fileName);
                    if (File.Exists(sourcePath) && !File.Exists(destPath))
                    {
                        File.Move(sourcePath, destPath);
                    }
                }

                if (!Directory.EnumerateFileSystemEntries(onnxSubDir).Any())
                {
                    Directory.Delete(onnxSubDir, false);
                }
            }

            // Verify required files exist
            var finalModelPath = Path.Combine(modelDirectory, onnxFileName);
            if (!File.Exists(finalModelPath))
            {
                throw new InvalidOperationException(
                    $"ONNX model file '{onnxFileName}' was not downloaded successfully in {modelDirectory}.");
            }

            if (!File.Exists(Path.Combine(modelDirectory, "tokenizer.json")))
            {
                throw new InvalidOperationException(
                    $"tokenizer.json not found in {modelDirectory}. " +
                    "The Harrier model requires tokenizer.json for tokenization.");
            }

            // Write SHA-256 sidecar for integrity verification
            var actualHash = ComputeSha256(finalModelPath);
            File.WriteAllText(finalModelPath + ".sha256", actualHash);
            if (!string.IsNullOrEmpty(_options.ExpectedHash) &&
                !string.Equals(actualHash, _options.ExpectedHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Hash mismatch for {finalModelPath}. Expected: {_options.ExpectedHash}, Actual: {actualHash}");
            }

            var dataFilePath = Path.Combine(modelDirectory, onnxDataFileName);
            if (File.Exists(dataFilePath))
            {
                WriteSidecarHash(dataFilePath);
            }

            return modelDirectory;
        }
        finally
        {
            downloadLock.Release();
        }
    }

    /// <summary>
    /// Gets the ONNX filename for the configured variant.
    /// </summary>
    internal static string GetOnnxFileName(HarrierModelVariant variant) => variant switch
    {
        HarrierModelVariant.Fp32 => "model.onnx",
        HarrierModelVariant.Fp16 => "model_fp16.onnx",
        HarrierModelVariant.Quantized => "model_quantized.onnx",
        HarrierModelVariant.Q4 => "model_q4.onnx",
        _ => "model.onnx"
    };

    /// <summary>
    /// Resolves the actual model file path, falling back through variants if the preferred one doesn't exist.
    /// </summary>
    internal static string ResolveModelPath(string modelDirectory, HarrierModelVariant variant)
    {
        var variantPath = Path.Combine(modelDirectory, GetOnnxFileName(variant));
        if (File.Exists(variantPath))
        {
            return variantPath;
        }

        // Fall back to default model.onnx
        var defaultPath = Path.Combine(modelDirectory, "model.onnx");
        if (File.Exists(defaultPath))
        {
            return defaultPath;
        }

        throw new FileNotFoundException(
            $"No ONNX model file found in {modelDirectory}. " +
            $"Expected '{GetOnnxFileName(variant)}' or 'model.onnx'.");
    }

    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void WriteSidecarHash(string filePath)
    {
        var hash = ComputeSha256(filePath);
        File.WriteAllText(filePath + ".sha256", hash);
    }

    private static bool SidecarHashValid(string filePath)
    {
        var sidecarPath = filePath + ".sha256";
        if (!File.Exists(sidecarPath))
        {
            return true;
        }

        var expectedHash = File.ReadAllText(sidecarPath).Trim();
        var actualHash = ComputeSha256(filePath);
        return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
