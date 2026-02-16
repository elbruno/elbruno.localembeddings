namespace ElBruno.LocalEmbeddings.ImageEmbeddings.Downloader;

/// <summary>
/// Interface for downloading image embedding models.
/// </summary>
public interface IImageModelDownloader
{
    /// <summary>
    /// Ensures that the required model files are present in the specified directory.
    /// Downloads them if they are missing.
    /// </summary>
    /// <param name="outputDirectory">The directory where model files should be stored.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if download fails.</exception>
    Task EnsureModelDownloadedAsync(string outputDirectory, CancellationToken cancellationToken = default);
}