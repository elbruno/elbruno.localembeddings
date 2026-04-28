using BenchmarkDotNet.Attributes;
using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Options;

namespace BenchmarkSample;

/// <summary>
/// Benchmarks for tokenizer throughput.
/// </summary>
[MemoryDiagnoser]
public class TokenizerBenchmarks
{
    private Tokenizer _tokenizer = null!;
    private string _shortText = null!;
    private string _longText = null!;
    private List<string> _batchTexts = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Resolve model directory (requires model to be pre-downloaded)
        var modelDir = ResolveModelDirectory();
        _tokenizer = new Tokenizer(modelDir);

        _shortText = "The quick brown fox jumps over the lazy dog.";
        _longText = string.Join(" ", Enumerable.Range(0, 50)
            .Select(i => $"Sentence number {i} is part of a longer text passage for tokenizer benchmarking."));

        _batchTexts = Enumerable.Range(0, 50)
            .Select(i => $"Sample sentence number {i} for batch tokenization benchmarking.")
            .ToList();
    }

    [Benchmark(Baseline = true, Description = "Tokenize short text")]
    public (long[], long[]) TokenizeShort()
        => _tokenizer.Tokenize(_shortText);

    [Benchmark(Description = "Tokenize long text")]
    public (long[], long[]) TokenizeLong()
        => _tokenizer.Tokenize(_longText);

    [Benchmark(Description = "Tokenize batch")]
    [Arguments(10)]
    [Arguments(50)]
    public (long[][], long[][]) TokenizeBatch(int count)
        => _tokenizer.TokenizeBatch(_batchTexts.Take(count));

    private static string ResolveModelDirectory()
    {
        var defaultCache = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LocalEmbeddings", "models")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "share", "LocalEmbeddings", "models");

        var modelDir = Path.Combine(defaultCache, "sentence-transformers_all-MiniLM-L6-v2");

        if (Directory.Exists(modelDir))
        {
            return modelDir;
        }

        // Fallback: download the model
        var options = new LocalEmbeddingsOptions
        {
            ModelName = "sentence-transformers/all-MiniLM-L6-v2",
            EnsureModelDownloaded = true
        };

        using var generator = new LocalEmbeddingGenerator(options);
        return modelDir;
    }
}
