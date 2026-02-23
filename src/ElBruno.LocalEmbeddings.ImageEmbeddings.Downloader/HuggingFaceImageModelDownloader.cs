using ElBruno.HuggingFace;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElBruno.LocalEmbeddings.ImageEmbeddings.Downloader;

/// <summary>
/// Implementation of <see cref="IImageModelDownloader"/> that downloads models from Hugging Face.
/// </summary>
public class HuggingFaceImageModelDownloader : IImageModelDownloader
{
    private readonly HuggingFaceDownloader _downloader;
    private readonly ILogger<HuggingFaceImageModelDownloader> _logger;
    private const string DefaultRepoId = "Xenova/clip-vit-base-patch32";

    /// <summary>
    /// Files required for the CLIP model.
    /// </summary>
    private static readonly string[] RequiredFiles =
    [
        "onnx/text_model.onnx",
        "onnx/vision_model.onnx",
        "vocab.json",
        "merges.txt"
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="HuggingFaceImageModelDownloader"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use for downloads.</param>
    /// <param name="logger">The logger.</param>
    public HuggingFaceImageModelDownloader(HttpClient httpClient, ILogger<HuggingFaceImageModelDownloader>? logger = null)
    {
        if (httpClient == null)
        {
            throw new ArgumentNullException(nameof(httpClient));
        }

        _downloader = new HuggingFaceDownloader(httpClient);
        _logger = logger ?? NullLogger<HuggingFaceImageModelDownloader>.Instance;
    }

    /// <inheritdoc />
    public async Task EnsureModelDownloadedAsync(string outputDirectory, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Output directory cannot be null or empty.", nameof(outputDirectory));
        }

        _logger.LogInformation("Ensuring CLIP model files are downloaded to {Directory}", outputDirectory);

        try
        {
            await _downloader.DownloadFilesAsync(new DownloadRequest
            {
                RepoId = DefaultRepoId,
                LocalDirectory = outputDirectory,
                RequiredFiles = RequiredFiles
            }, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully ensured all CLIP model files are present in {Directory}", outputDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download CLIP model files to {Directory}", outputDirectory);
            throw new InvalidOperationException($"Failed to download CLIP model files from {DefaultRepoId}.", ex);
        }
    }
}
