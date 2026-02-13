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
    /// <param name="progress">Optional progress reporter (0.0 to 1.0).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The local path to the model directory.</returns>
    Task<string> EnsureModelAsync(string modelName, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

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
    private const string HuggingFaceBaseUrl = "https://huggingface.co";

    private static readonly string[] TokenizerFiles = ["tokenizer.json", "tokenizer_config.json", "vocab.txt"];

    private readonly HttpClient _httpClient;
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
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _cacheDirectory = cacheDirectory ?? GetDefaultCacheDirectory();
    }

    /// <summary>
    /// Gets the default HuggingFace model name.
    /// </summary>
    public static string DefaultModelName => DefaultModel;

    /// <inheritdoc />
    public async Task<string> EnsureModelAsync(string modelName, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            throw new ArgumentException("Model name cannot be null or empty.", nameof(modelName));
        }

        var sanitizedName = SanitizeModelName(modelName);
        var modelDirectory = Path.Combine(_cacheDirectory, sanitizedName);
        var modelPath = Path.Combine(modelDirectory, "model.onnx");

        // Create directory if it doesn't exist
        Directory.CreateDirectory(modelDirectory);

        // Track total files to download for progress
        var filesToDownload = new List<(string url, string localPath, bool required)>
        {
            (GetOnnxModelUrl(modelName), modelPath, true)
        };

        // Add tokenizer files
        foreach (var tokenizerFile in TokenizerFiles)
        {
            filesToDownload.Add((GetTokenizerFileUrl(modelName, tokenizerFile), Path.Combine(modelDirectory, tokenizerFile), false));
        }

        var completedFiles = 0;
        var totalFiles = filesToDownload.Count;

        foreach (var (url, localPath, required) in filesToDownload)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(localPath))
            {
                try
                {
                    var fileProgress = progress is not null
                        ? new Progress<double>(p => progress.Report((completedFiles + p) / totalFiles))
                        : null;

                    await DownloadFileAsync(url, localPath, fileProgress, cancellationToken).ConfigureAwait(false);
                }
                catch (HttpRequestException) when (!required)
                {
                    // Optional files (tokenizer files) can fail silently
                    // Some models may not have all tokenizer files
                }
                catch (HttpRequestException ex) when (required)
                {
                    throw new InvalidOperationException($"Failed to download required model file from '{url}': {ex.Message}", ex);
                }
            }

            completedFiles++;
            progress?.Report((double)completedFiles / totalFiles);
        }

        // Verify the ONNX model was downloaded
        if (!File.Exists(modelPath))
        {
            throw new InvalidOperationException($"Model file was not downloaded successfully: {modelPath}");
        }

        return modelDirectory;
    }

    /// <inheritdoc />
    public string GetCacheDirectory() => _cacheDirectory;

    private static string GetDefaultCacheDirectory()
    {
        // Windows: %LOCALAPPDATA%\LocalEmbeddings\models
        // Linux/macOS: ~/.local/share/LocalEmbeddings/models

        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(localAppData))
            {
                // Fallback for Windows
                localAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local");
            }
            return Path.Combine(localAppData, "LocalEmbeddings", "models");
        }
        else
        {
            // Linux/macOS: Use XDG_DATA_HOME or fallback to ~/.local/share
            var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (string.IsNullOrEmpty(dataHome))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrEmpty(home))
                {
                    home = Environment.GetEnvironmentVariable("HOME") ?? "/tmp";
                }
                dataHome = Path.Combine(home, ".local", "share");
            }
            return Path.Combine(dataHome, "LocalEmbeddings", "models");
        }
    }

    private static string SanitizeModelName(string modelName)
    {
        // Replace characters that are invalid in file paths
        return modelName
            .Replace('/', '_')
            .Replace('\\', '_')
            .Replace(':', '_')
            .Replace('*', '_')
            .Replace('?', '_')
            .Replace('"', '_')
            .Replace('<', '_')
            .Replace('>', '_')
            .Replace('|', '_');
    }

    private static string GetOnnxModelUrl(string modelName)
        => $"{HuggingFaceBaseUrl}/{modelName}/resolve/main/onnx/model.onnx";

    private static string GetTokenizerFileUrl(string modelName, string fileName)
        => $"{HuggingFaceBaseUrl}/{modelName}/resolve/main/{fileName}";

    private async Task DownloadFileAsync(string url, string destinationPath, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to download file. Status: {response.StatusCode}, URL: {url}");
        }

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        var downloadedBytes = 0L;

        // Use a temp file to avoid partial downloads
        var tempPath = destinationPath + ".tmp";

        try
        {
            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);

            var buffer = new byte[81920];
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                downloadedBytes += bytesRead;

                if (totalBytes > 0)
                {
                    progress?.Report((double)downloadedBytes / totalBytes);
                }
            }

            // Ensure all data is written
            await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Clean up temp file on failure
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
            throw;
        }

        // Move temp file to final destination
        File.Move(tempPath, destinationPath, overwrite: true);
    }
}
