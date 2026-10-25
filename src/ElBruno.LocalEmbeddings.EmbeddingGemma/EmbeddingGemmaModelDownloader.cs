using System.Collections.Concurrent;
using System.Security.Cryptography;
using ElBruno.HuggingFace;

namespace ElBruno.LocalEmbeddings.EmbeddingGemma;

/// <summary>
/// Downloads the pinned EmbeddingGemma 2 Q4 ONNX model and Google's SentencePiece tokenizer.
/// </summary>
public sealed class EmbeddingGemmaModelDownloader
{
    internal const string OnnxRepository = "onnx-community/embeddinggemma-2-ONNX";
    internal const string OnnxRevision = "daa72c51243991dfcaf9f9137d2c573d8f7790c0";
    internal const string TokenizerRepository = "google/embeddinggemma-2";
    internal const string TokenizerRevision = "914f7f89142e33e77833254d9c9b90c3cef7303b";
    internal const string ModelRelativePath = "onnx/model_q4.onnx";
    internal const string ModelDataRelativePath = "onnx/model_q4.onnx_data";
    internal const string TokenizerFileName = "tokenizer.model";

    private const string ModelSha256 = "f9eeba97acddf139b8ee2ddf04bc30dceafa88de93fadf74d7644e0d61a477a9";
    private const string ModelDataSha256 = "c3975f2d1ab7a1878ae31a7d7a9b7804a827aff3800b60dfceafce21cac3df49";
    private const string TokenizerSha256 = "e594c8a90eb08d8bda498ff4747977dc827ae0c3c56b5c0d41a605a22d02ef03";

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> DownloadLocks = new(StringComparer.OrdinalIgnoreCase);

    private readonly HuggingFaceDownloader _downloader;
    private readonly string _cacheDirectory;

    /// <summary>
    /// Creates a downloader using the specified HTTP client and cache directory.
    /// </summary>
    public EmbeddingGemmaModelDownloader(HttpClient httpClient, string? cacheDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _downloader = new HuggingFaceDownloader(httpClient);
        _cacheDirectory = cacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ElBruno", "LocalEmbeddings", "models");
    }

    /// <summary>
    /// Ensures the pinned ONNX model, external weights, and tokenizer are downloaded and verified.
    /// </summary>
    public async Task<string> EnsureModelAsync(
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var modelDirectory = Path.GetFullPath(Path.Combine(_cacheDirectory, "embeddinggemma-2"));
        var cacheRoot = Path.GetFullPath(_cacheDirectory);
        if (!modelDirectory.StartsWith(
            cacheRoot.EndsWith(Path.DirectorySeparatorChar) ? cacheRoot : cacheRoot + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("EmbeddingGemma model cache path resolves outside the configured cache directory.");
        }

        // A call that is already cancelled ends here, also when the model is complete, with the TaskCanceledException of a cancelled wait.
        if (cancellationToken.IsCancellationRequested)
        {
            return await Task.FromCanceled<string>(cancellationToken).ConfigureAwait(false);
        }

        // A complete model returns at once, without the lock file.
        if (ModelDownloadLock.IsComplete(() => FilesAreValid(modelDirectory)))
        {
            return modelDirectory;
        }

        // The progress of a download is posted to the context of the caller, which the Progress<T> captures here, before any await.
        var downloadProgress = progress is null
            ? null
            : new Progress<DownloadProgress>(p => progress.Report(p.PercentComplete / 100.0));

        var downloadLock = DownloadLocks.GetOrAdd(modelDirectory, static _ => new SemaphoreSlim(1, 1));
        await downloadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // The semaphore covers this process; the lock file in the model directory covers other processes that use the same cache.
            // Another process may have finished the download while this one waited, so the files are checked again.
            using var processLock = await ModelDownloadLock.AcquireAsync(modelDirectory, cancellationToken).ConfigureAwait(false);
            if (FilesAreValid(modelDirectory))
            {
                return modelDirectory;
            }

            await _downloader.DownloadFilesAsync(new DownloadRequest
            {
                RepoId = OnnxRepository,
                Revision = OnnxRevision,
                LocalDirectory = modelDirectory,
                RequiredFiles = [ModelRelativePath, ModelDataRelativePath],
                Progress = downloadProgress,
                UseAtomicWrites = true
            }, cancellationToken).ConfigureAwait(false);

            await _downloader.DownloadFilesAsync(new DownloadRequest
            {
                RepoId = TokenizerRepository,
                Revision = TokenizerRevision,
                LocalDirectory = modelDirectory,
                RequiredFiles = [TokenizerFileName],
                Progress = downloadProgress,
                UseAtomicWrites = true
            }, cancellationToken).ConfigureAwait(false);

            ValidateFiles(modelDirectory);
            return modelDirectory;
        }
        finally
        {
            downloadLock.Release();
        }
    }

    internal static string GetModelPath(string modelDirectory)
        => Path.Combine(modelDirectory, ModelRelativePath.Replace('/', Path.DirectorySeparatorChar));

    internal static bool FilesAreValid(string modelDirectory)
    {
        var modelPath = GetModelPath(modelDirectory);
        var dataPath = Path.Combine(Path.GetDirectoryName(modelPath)!, Path.GetFileName(ModelDataRelativePath));
        var tokenizerPath = Path.Combine(modelDirectory, TokenizerFileName);
        return HasExpectedHash(modelPath, ModelSha256)
            && HasExpectedHash(dataPath, ModelDataSha256)
            && HasExpectedHash(tokenizerPath, TokenizerSha256);
    }

    private static void ValidateFiles(string modelDirectory)
    {
        var modelPath = GetModelPath(modelDirectory);
        var dataPath = Path.Combine(Path.GetDirectoryName(modelPath)!, Path.GetFileName(ModelDataRelativePath));
        var tokenizerPath = Path.Combine(modelDirectory, TokenizerFileName);

        ValidateHash(modelPath, ModelSha256);
        ValidateHash(dataPath, ModelDataSha256);
        ValidateHash(tokenizerPath, TokenizerSha256);
    }

    private static void ValidateHash(string path, string expectedHash)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("A required EmbeddingGemma model file is missing.", path);
        }

        var actualHash = ComputeSha256(path);
        if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"SHA-256 verification failed for '{path}'. Expected {expectedHash}, received {actualHash}.");
        }
    }

    private static bool HasExpectedHash(string path, string expectedHash)
        => File.Exists(path) &&
           string.Equals(ComputeSha256(path), expectedHash, StringComparison.OrdinalIgnoreCase);

    private static string ComputeSha256(string path)
    {
        using var stream = ModelDownloadLock.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
