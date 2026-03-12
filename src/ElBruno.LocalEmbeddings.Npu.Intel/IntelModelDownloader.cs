using System.Collections.Concurrent;
using System.Security.Cryptography;
using ElBruno.HuggingFace;

namespace ElBruno.LocalEmbeddings.Npu.Intel;

/// <summary>
/// Internal model downloader for the Intel NPU library.
/// Downloads and caches ONNX models from HuggingFace Hub.
/// </summary>
/// <remarks>
/// This is a standalone copy to avoid referencing the base ElBruno.LocalEmbeddings
/// library, which would cause OnnxRuntime version conflicts with Intel OpenVINO.
/// </remarks>
internal sealed class IntelModelDownloader
{
    private static readonly string[] QuantizedModelFiles = ["model_quantized.onnx", "model_int8.onnx"];
    private static readonly string[] TokenizerFiles = ["tokenizer.json", "tokenizer_config.json", "vocab.txt"];
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> DownloadLocks = new(StringComparer.OrdinalIgnoreCase);

    private readonly HuggingFaceDownloader _downloader;
    private readonly string _cacheDirectory;

    public IntelModelDownloader(HttpClient httpClient, string? cacheDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        _downloader = new HuggingFaceDownloader(httpClient);
        _cacheDirectory = cacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ElBruno", "LocalEmbeddings", "models");
    }

    public async Task<string> EnsureModelAsync(
        string modelName,
        bool preferQuantized = false,
        IProgress<double>? progress = null,
        string? expectedHash = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            throw new ArgumentException("Model name cannot be null or empty.", nameof(modelName));
        }

        var sanitizedName = DefaultPathHelper.SanitizeModelName(modelName);
        var modelDirectory = Path.GetFullPath(Path.Combine(_cacheDirectory, sanitizedName));
        var cacheRoot = Path.GetFullPath(_cacheDirectory);
        if (!modelDirectory.StartsWith(cacheRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Model name '{modelName}' resolves to a path outside the cache directory.",
                nameof(modelName));
        }

        var sem = DownloadLocks.GetOrAdd(modelDirectory, _ => new SemaphoreSlim(1, 1));
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

    private async Task<string> EnsureModelCoreAsync(
        string modelDirectory, string modelName, bool preferQuantized,
        IProgress<double>? progress, string? expectedHash, CancellationToken cancellationToken)
    {
        var requiredFiles = new List<string>();
        var optionalFiles = new List<string>(TokenizerFiles);

        bool existingDefaultModel = File.Exists(Path.Combine(modelDirectory, "model.onnx"));
        var existingQuantizedModel = QuantizedModelFiles
            .FirstOrDefault(f => File.Exists(Path.Combine(modelDirectory, f)));

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
            if (preferQuantized)
            {
                foreach (var quantizedFile in QuantizedModelFiles)
                {
                    optionalFiles.Insert(0, $"onnx/{quantizedFile}");
                }
                requiredFiles.Add("onnx/model.onnx");
            }
            else
            {
                requiredFiles.Add("onnx/model.onnx");
            }
        }

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
            RepoId = modelName,
            LocalDirectory = modelDirectory,
            RequiredFiles = requiredFiles,
            OptionalFiles = optionalFiles,
            Progress = downloadProgress
        }, cancellationToken).ConfigureAwait(false);

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

        var finalDefaultModel = File.Exists(Path.Combine(modelDirectory, "model.onnx"));
        var finalQuantizedModel = QuantizedModelFiles
            .FirstOrDefault(f => File.Exists(Path.Combine(modelDirectory, f)));

        if (!finalDefaultModel && finalQuantizedModel == null)
        {
            throw new InvalidOperationException($"Model file was not downloaded successfully in {modelDirectory}");
        }

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

        var expected = File.ReadAllText(sidecarPath).Trim();
        var actual = ComputeSha256(filePath);
        return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
    }
}
