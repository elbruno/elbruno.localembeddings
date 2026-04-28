namespace ElBruno.LocalEmbeddings.Benchmarks;

internal static class BenchmarkHelpers
{
    private const string ModelCacheSubDir = "sentence-transformers_all-MiniLM-L6-v2";
    private const string HarrierModelCacheSubDir = "onnx-community_harrier-oss-v1-270m-ONNX";

    /// <summary>
    /// Returns the local model directory if it exists on disk, or null when no model is cached.
    /// </summary>
    internal static string? TryResolveModelDirectory()
    {
        var cacheDir = ResolveBaseCacheDir();
        var modelDir = Path.Combine(cacheDir, ModelCacheSubDir);
        return Directory.Exists(modelDir) ? modelDir : null;
    }

    /// <summary>
    /// Returns the local Harrier model directory if it exists on disk, or null when no model is cached.
    /// </summary>
    internal static string? TryResolveHarrierModelDirectory()
    {
        var cacheDir = ResolveHarrierCacheDir();
        var modelDir = Path.Combine(cacheDir, HarrierModelCacheSubDir);

        if (!Directory.Exists(modelDir))
            return null;

        // Verify tokenizer.json exists (required for Harrier)
        return File.Exists(Path.Combine(modelDir, "tokenizer.json")) ? modelDir : null;
    }

    private static string ResolveBaseCacheDir() =>
        OperatingSystem.IsWindows()
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LocalEmbeddings", "models")
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "share", "LocalEmbeddings", "models");

    private static string ResolveHarrierCacheDir() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ElBruno", "LocalEmbeddings", "models");
}
