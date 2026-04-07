using ElBruno.LocalEmbeddings.Harrier;
using ElBruno.LocalEmbeddings.Harrier.Options;
using ElBruno.LocalEmbeddings.Options;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalEmbeddings.SharedModelTests;

/// <summary>
/// Provides model availability detection and lazy-initialized generators
/// for all supported embedding models.
/// </summary>
public static class ModelFixture
{
    private static readonly string CacheRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ElBruno", "LocalEmbeddings", "models");

    // Legacy cache path (pre-ElBruno prefix)
    private static readonly string LegacyCacheRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LocalEmbeddings", "models");

    private static readonly Lazy<LocalEmbeddingGenerator?> MiniLmGenerator = new(CreateMiniLmGenerator);
    private static readonly Lazy<HarrierEmbeddingGenerator?> HarrierGenerator = new(CreateHarrierGenerator);

    /// <summary>
    /// Returns the MiniLM generator if the model is available, or null.
    /// </summary>
    public static LocalEmbeddingGenerator? GetMiniLmGenerator() => MiniLmGenerator.Value;

    /// <summary>
    /// Returns the Harrier generator if the model is available, or null.
    /// </summary>
    public static HarrierEmbeddingGenerator? GetHarrierGenerator() => HarrierGenerator.Value;

    /// <summary>
    /// Enumerates all locally available generators as (name, generator) tuples.
    /// </summary>
    public static IEnumerable<(string Name, IEmbeddingGenerator<string, Embedding<float>> Generator)> GetAvailableGenerators()
    {
        var miniLm = GetMiniLmGenerator();
        if (miniLm is not null)
            yield return ("MiniLM-L6-v2", miniLm);

        var harrier = GetHarrierGenerator();
        if (harrier is not null)
            yield return ("Harrier-270M", harrier);
    }

    private static string? GetMiniLmModelPath()
    {
        // Check new cache path first
        var modelDir = Path.Combine(CacheRoot, "sentence-transformers_all-MiniLM-L6-v2");
        if (Directory.Exists(modelDir) && File.Exists(Path.Combine(modelDir, "model.onnx")))
            return modelDir;

        // Check legacy cache path
        modelDir = Path.Combine(LegacyCacheRoot, "sentence-transformers_all-MiniLM-L6-v2");
        if (Directory.Exists(modelDir) && File.Exists(Path.Combine(modelDir, "model.onnx")))
            return modelDir;

        var envPath = Environment.GetEnvironmentVariable("LOCALEMBEDDINGS_TEST_MODEL");
        return !string.IsNullOrEmpty(envPath) && Directory.Exists(envPath) ? envPath : null;
    }

    private static string? GetHarrierModelPath()
    {
        var modelDir = Path.Combine(CacheRoot, "onnx-community_harrier-oss-v1-270m-ONNX");

        // Check for any ONNX model variant
        if (Directory.Exists(modelDir) &&
            File.Exists(Path.Combine(modelDir, "tokenizer.json")) &&
            (File.Exists(Path.Combine(modelDir, "model.onnx")) ||
             File.Exists(Path.Combine(modelDir, "model_quantized.onnx")) ||
             File.Exists(Path.Combine(modelDir, "model_q4.onnx")) ||
             File.Exists(Path.Combine(modelDir, "model_fp16.onnx"))))
        {
            return modelDir;
        }

        var envPath = Environment.GetEnvironmentVariable("HARRIER_TEST_MODEL");
        return !string.IsNullOrEmpty(envPath) && Directory.Exists(envPath) ? envPath : null;
    }

    private static LocalEmbeddingGenerator? CreateMiniLmGenerator()
    {
        var modelPath = GetMiniLmModelPath();
        if (modelPath is null) return null;

        try
        {
            return new LocalEmbeddingGenerator(new LocalEmbeddingsOptions
            {
                ModelPath = modelPath,
                EnsureModelDownloaded = false
            });
        }
        catch
        {
            return null;
        }
    }

    private static HarrierEmbeddingGenerator? CreateHarrierGenerator()
    {
        var modelPath = GetHarrierModelPath();
        if (modelPath is null) return null;

        try
        {
            return HarrierEmbeddingGenerator.CreateAsync(new HarrierEmbeddingsOptions
            {
                ModelPath = modelPath,
                EnsureModelDownloaded = false
            }).GetAwaiter().GetResult();
        }
        catch
        {
            return null;
        }
    }
}
