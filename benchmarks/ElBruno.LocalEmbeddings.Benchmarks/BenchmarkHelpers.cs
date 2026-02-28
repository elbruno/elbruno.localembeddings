namespace ElBruno.LocalEmbeddings.Benchmarks;

internal static class BenchmarkHelpers
{
    private const string ModelCacheSubDir = "sentence-transformers_all-MiniLM-L6-v2";

    /// <summary>
    /// Returns the local model directory if it exists on disk, or null when no model is cached.
    /// </summary>
    internal static string? TryResolveModelDirectory()
    {
        var cacheDir = OperatingSystem.IsWindows()
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LocalEmbeddings", "models")
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "share", "LocalEmbeddings", "models");

        var modelDir = Path.Combine(cacheDir, ModelCacheSubDir);
        return Directory.Exists(modelDir) ? modelDir : null;
    }
}
