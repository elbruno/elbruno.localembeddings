using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElBruno.LocalEmbeddings.ImageEmbeddings.Downloader;

/// <summary>
/// Implementation of <see cref="IImageModelDownloader"/> that downloads models from Hugging Face.
/// </summary>
public class HuggingFaceImageModelDownloader : IImageModelDownloader
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HuggingFaceImageModelDownloader> _logger;
    private const string DefaultBaseUrl = "https://huggingface.co/Xenova/clip-vit-base-patch32/resolve/main";

    /// <summary>
    /// Files required for the CLIP model.
    /// Structure: LocalFileName -> RemoteRelativePath
    /// </summary>
    private static readonly Dictionary<string, string> RequiredFiles = new()
    {
        { "text_model.onnx", "onnx/text_model.onnx" },
        { "vision_model.onnx", "onnx/vision_model.onnx" },
        { "vocab.json", "vocab.json" },
        { "merges.txt", "merges.txt" }
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="HuggingFaceImageModelDownloader"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for downloads.</param>
    /// <param name="logger">The logger.</param>
    public HuggingFaceImageModelDownloader(HttpClient httpClient, ILogger<HuggingFaceImageModelDownloader>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? NullLogger<HuggingFaceImageModelDownloader>.Instance;
    }

    /// <inheritdoc />
    public async Task EnsureModelDownloadedAsync(string outputDirectory, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Output directory cannot be null or empty.", nameof(outputDirectory));
        }

        if (!Directory.Exists(outputDirectory))
        {
            _logger.LogInformation("Creating model directory: {Directory}", outputDirectory);
            Directory.CreateDirectory(outputDirectory);
        }

        foreach (var file in RequiredFiles)
        {
            var localFileName = file.Key;
            var remotePath = file.Value;
            var localPath = Path.Combine(outputDirectory, localFileName);
            var remoteUrl = $"{DefaultBaseUrl}/{remotePath}";

            if (File.Exists(localPath))
            {
                _logger.LogDebug("File {FileName} already exists. Skipping download.", localFileName);
                continue;
            }

            _logger.LogInformation("Downloading {FileName} from {Url}...", localFileName, remoteUrl);

            try
            {
                using var response = await _httpClient.GetAsync(remoteUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None);

                await stream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Successfully downloaded {FileName}.", localFileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download {FileName}.", localFileName);
                // Clean up partial file if it exists
                if (File.Exists(localPath))
                {
                    try { File.Delete(localPath); } catch { /* Ignore cleanup errors */ }
                }
                throw new InvalidOperationException($"Failed to download {localFileName} from {remoteUrl}.", ex);
            }
        }
    }
}