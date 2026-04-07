using BenchmarkDotNet.Attributes;
using ElBruno.LocalEmbeddings.Harrier;

namespace ElBruno.LocalEmbeddings.Benchmarks;

/// <summary>Harrier tokenizer allocation and throughput benchmarks.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class HarrierTokenizerBenchmarks
{
    private HarrierTokenizer? _tokenizer;
    private HarrierTokenizer? _tokenizerNoPrefix;
    private string _shortText = "Hello world";
    private string _longText = string.Empty;
    private string[] _batch10 = [];

    [GlobalSetup]
    public void Setup()
    {
        _longText = string.Join(" ", Enumerable.Range(0, 50)
            .Select(i => $"Sentence number {i} for tokenization benchmarking with extended text content that simulates realistic paragraph input."));

        _batch10 = Enumerable.Range(0, 10)
            .Select(i => $"Batch item {i}: The quick brown fox jumps over the lazy dog.")
            .ToArray();

        var modelDir = BenchmarkHelpers.TryResolveHarrierModelDirectory();
        if (modelDir is null) return;

        try
        {
            _tokenizer = HarrierTokenizer.Create(modelDir, maxLength: 8192);
            _tokenizerNoPrefix = HarrierTokenizer.Create(modelDir, maxLength: 8192, instructionPrefix: null);
        }
        catch (Exception)
        {
            _tokenizer = null;
            _tokenizerNoPrefix = null;
        }
    }

    /// <summary>Tokenize a short text (~3 tokens). Measures per-call allocation baseline.</summary>
    [Benchmark]
    public (long[], long[]) TokenizeShortText()
    {
        if (_tokenizer is null) return ([], []);
        return _tokenizer.Tokenize(_shortText);
    }

    /// <summary>Tokenize a 500-word paragraph. Measures full-length tokenization cost.</summary>
    [Benchmark]
    public (long[], long[]) TokenizeLongText()
    {
        if (_tokenizer is null) return ([], []);
        return _tokenizer.Tokenize(_longText);
    }

    /// <summary>Tokenize 10 items in batch. Measures batch overhead.</summary>
    [Benchmark]
    public (long[][], long[][]) TokenizeBatch10()
    {
        if (_tokenizer is null) return ([], []);
        return _tokenizer.TokenizeBatch(_batch10);
    }

    /// <summary>Tokenize with default instruction prefix. Measures prefix concatenation cost.</summary>
    [Benchmark]
    public (long[], long[]) TokenizeWithPrefix()
    {
        if (_tokenizer is null) return ([], []);
        return _tokenizer.Tokenize(_shortText);
    }

    /// <summary>Tokenize without instruction prefix. Comparison baseline for prefix overhead.</summary>
    [Benchmark]
    public (long[], long[]) TokenizeWithoutPrefix()
    {
        if (_tokenizerNoPrefix is null) return ([], []);
        return _tokenizerNoPrefix.Tokenize(_shortText);
    }

    /// <summary>CountTokens path — measures overhead of full tokenize + count.</summary>
    [Benchmark]
    public int CountTokens()
    {
        if (_tokenizer is null) return 0;
        return _tokenizer.CountTokens(_shortText);
    }
}
