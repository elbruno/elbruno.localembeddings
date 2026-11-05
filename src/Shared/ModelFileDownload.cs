using ElBruno.HuggingFace;

namespace ElBruno.LocalEmbeddings;

/// <summary>
/// Calls the downloader for the files that a model directory does not have.
/// </summary>
internal static class ModelFileDownload
{
    /// <summary>
    /// Returns without any request when every file of the request exists. Otherwise it removes the metadata of the previous download and
    /// downloads the missing files: the downloader refuses a directory whose metadata names another repository or an older commit,
    /// while a model directory takes files from two repositories, and is completed also after the repository has moved.
    /// </summary>
    public static async Task DownloadMissingFilesAsync(this HuggingFaceDownloader downloader, DownloadRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var files = request.RequiredFiles.Concat(request.OptionalFiles ?? []);
        if (files.All(file => File.Exists(Path.Combine(request.LocalDirectory, file.Replace('/', Path.DirectorySeparatorChar)))))
        {
            request.Progress?.Report(new DownloadProgress
            {
                Stage = DownloadStage.Complete,
                PercentComplete = 100,
                Message = "All files already present."
            });
            return;
        }

        var metadataPath = Path.Combine(request.LocalDirectory, HuggingFaceMetadataFileNames.DownloadResolutionMetadata);
        if (File.Exists(metadataPath))
        {
            File.Delete(metadataPath);
        }

        await downloader.DownloadFilesAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
