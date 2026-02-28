using System.Collections.Concurrent;
using System.Security.Cryptography;
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
    /// <param name="expectedHash">Optional expected SHA-256 hash (hex string) of the primary ONNX model file.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The local path to the model directory.</returns>
    Task<string> EnsureModelAsync(string modelName, bool preferQuantized = false, IProgress<double>? progress = null, string? expectedHash = null, CancellationToken cancellationToken = default);

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
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _downloadLocks = new(StringComparer.OrdinalIgnoreCase);

    private readonly HuggingFaceDownloader _downloader;
    private readonly string _cacheDirectory;

    /// <summary>
    /// Creates a new ModelDownloader with default settings.
    /// </summary>
    /// <remarks>
    /// For production or long-running processes, prefer the constructor overload that accepts an
    /// <see cref="System.Net.Http.IHttpClientFactory"/>-managed <see cref="System.Net.Http.HttpClient"/>
    /// (via DI / <c>AddModelDownloader()</c>) to benefit from connection pooling and lifecycle management.
    /// </remarks>
    public ModelDownloader() : this(
        new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) }),
        null)
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
        _cacheDirectory = cacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ElBruno", "LocalEmbeddings", "models");
    }

    /// <summary>
    /// Gets the default HuggingFace model name.
    /// </summary>
    public static string DefaultModelName => DefaultModel;

    /// <inheritdoc />
    public async Task<string> EnsureModelAsync(string modelName, bool preferQuantized = false, IProgress<double>? progress = null, string? expectedHash = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            throw new ArgumentException("Model name cannot be null or empty.", nameof(modelName));
        }

        // SEC-006: defense-in-depth path traversal guard
        var sanitizedName = DefaultPathHelper.SanitizeModelName(modelName);
        var modelDirectory = Path.GetFullPath(Path.Combine(_cacheDirectory, sanitizedName));
        var cacheRoot = Path.GetFullPath(_cacheDirectory);
        if (!modelDirectory.StartsWith(cacheRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Model name '{modelName}' resolves to a path outside the cache directory.",
                nameof(modelName));
        }

        // Serialize concurrent downloads for the same model directory to avoid .tmp file conflicts.
        var sem = _downloadLocks.GetOrAdd(modelDirectory, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await EnsureModelCoreAsync(modelDirectory, modelName, preferQuantized, progress, expectedHash, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            sem.Release();
        }
    }

    private async Task<string> EnsureModelCoreAsync(string modelDirectory, string modelName, bool preferQuantized, IProgress<double>? progress, string? expectedHash, CancellationToken cancellationToken)
    {
        var requiredFiles = new List<string>();
        var optionalFiles = new List<string>(TokenizerFiles.Select(f => f));

        // Determine which ONNX model file to download
        // Check if any model already exists locally
        bool existingDefaultModel = File.Exists(Path.Combine(modelDirectory, "model.onnx"));
        var existingQuantizedModel = QuantizedModelFiles
            .FirstOrDefault(f => File.Exists(Path.Combine(modelDirectory, f)));

        // SEC-001: verify sidecar hash integrity for any cached ONNX files; delete if corrupted
        if (existingDefaultModel)
        {
            var path = Path.Combine(modelDirectory, "model.onnx");
            if (!SidecarHashValid(path))
            {
                File.Delete(path);
                existingDefaultModel = false;
            }
        }

        if (existingQuantizedModel != null)
        {
            var path = Path.Combine(modelDirectory, existingQuantizedModel);
            if (!SidecarHashValid(path))
            {
                File.Delete(path);
                existingQuantizedModel = null;
            }
        }

        bool hasExistingModel = existingDefaultModel || existingQuantizedModel != null;
        if (!hasExistingModel)
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

        // The downloader preserves subdirectory structure (e.g., onnx/model.onnx).
        // Move ONNX files to the model directory root for backward compatibility.
        var onnxSubDir = Path.Combine(modelDirectory, "onnx");
        if (Directory.Exists(onnxSubDir))
        {
            foreach (var file in Directory.GetFiles(onnxSubDir, "*.onnx"))
            {
                var destPath = Path.Combine(modelDirectory, Path.GetFileName(file));
                if (!File.Exists(destPath))
                {
                    File.Move(file, destPath);
                }
            }
        }

        // Verify at least one ONNX model file exists
        var finalDefaultModel = File.Exists(Path.Combine(modelDirectory, "model.onnx"));
        var finalQuantizedModel = QuantizedModelFiles
            .FirstOrDefault(f => File.Exists(Path.Combine(modelDirectory, f)));

        if (!finalDefaultModel && finalQuantizedModel == null)
        {
            throw new InvalidOperationException($"Model file was not downloaded successfully in {modelDirectory}");
        }

        // SEC-001: write (or refresh) sidecar SHA-256 hashes for all ONNX files present
        var defaultModelPath = Path.Combine(modelDirectory, "model.onnx");
        if (File.Exists(defaultModelPath))
        {
            WriteSidecarHash(defaultModelPath);
        }

        foreach (var qf in QuantizedModelFiles)
        {
            var qPath = Path.Combine(modelDirectory, qf);
            if (File.Exists(qPath))
            {
                WriteSidecarHash(qPath);
            }
        }

        // SEC-001: verify against caller-supplied expected hash if provided
        if (expectedHash != null)
        {
            string? primaryModelPath = null;
            if (preferQuantized)
            {
                primaryModelPath = QuantizedModelFiles
                    .Select(f => Path.Combine(modelDirectory, f))
                    .FirstOrDefault(File.Exists);
            }

            primaryModelPath ??= defaultModelPath;

            if (File.Exists(primaryModelPath))
            {
                var actualHash = ComputeSha256(primaryModelPath);
                if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Model file hash verification failed. Expected: {expectedHash}, Actual: {actualHash}.");
                }
            }
        }

        return modelDirectory;
    }

    /// <inheritdoc />
    public string GetCacheDirectory() => _cacheDirectory;

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

    /// <summary>Returns true when the sidecar is absent (legacy) or its hash matches the file.</summary>
    private static bool SidecarHashValid(string filePath)
    {
        var sidecarPath = filePath + ".sha256";
        if (!File.Exists(sidecarPath))
        {
            return true; // no sidecar — legacy cached file, treat as valid
        }

        var expected = File.ReadAllText(sidecarPath).Trim();
        var actual = ComputeSha256(filePath);
        return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
    }
}
