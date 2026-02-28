using System.Security.Cryptography;
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
    /// ONNX model files that live in the output directory root after download.
    /// </summary>
    private static readonly string[] OnnxModelFileNames = ["text_model.onnx", "vision_model.onnx"];

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
            // SEC-001: verify sidecar hash integrity for any cached ONNX files; delete if corrupted
            foreach (var onnxFile in OnnxModelFileNames)
            {
                var filePath = Path.Combine(outputDirectory, onnxFile);
                if (File.Exists(filePath) && !SidecarHashValid(filePath))
                {
                    _logger.LogWarning("Integrity check failed for {File}; re-downloading.", onnxFile);
                    File.Delete(filePath);
                }
            }

            await _downloader.DownloadFilesAsync(new DownloadRequest
            {
                RepoId = DefaultRepoId,
                LocalDirectory = outputDirectory,
                RequiredFiles = RequiredFiles
            }, cancellationToken).ConfigureAwait(false);

            // The downloader preserves subdirectory structure (e.g., onnx/text_model.onnx).
            // Move ONNX files to the output directory root for backward compatibility.
            var onnxSubDir = Path.Combine(outputDirectory, "onnx");
            if (Directory.Exists(onnxSubDir))
            {
                foreach (var file in Directory.GetFiles(onnxSubDir, "*.onnx"))
                {
                    var destPath = Path.Combine(outputDirectory, Path.GetFileName(file));
                    if (!File.Exists(destPath))
                    {
                        File.Move(file, destPath);
                    }
                }
            }

            // SEC-001: write (or refresh) sidecar SHA-256 hashes for ONNX files
            foreach (var onnxFile in OnnxModelFileNames)
            {
                var filePath = Path.Combine(outputDirectory, onnxFile);
                if (File.Exists(filePath))
                {
                    WriteSidecarHash(filePath);
                }
            }

            _logger.LogInformation("Successfully ensured all CLIP model files are present in {Directory}", outputDirectory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download CLIP model files to {Directory}", outputDirectory);
            throw new InvalidOperationException($"Failed to download CLIP model files from {DefaultRepoId}.", ex);
        }
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
