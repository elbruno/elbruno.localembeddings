using BenchmarkDotNet.Attributes;
using ElBruno.LocalEmbeddings;

namespace ElBruno.LocalEmbeddings.Benchmarks;

/// <summary>Tokenizer allocation profiling benchmarks.</summary>
[MemoryDiagnoser]
public class TokenizerBenchmarks
{
    private Tokenizer? _tokenizer;
    private string _shortText = "Hello world";
    private string _longText = string.Empty;

    [GlobalSetup]
    public void Setup()
    {
        _longText = string.Join(" ", Enumerable.Range(0, 50)
            .Select(i => $"Sentence number {i} for tokenization benchmarking with extended text content."));

        var modelDir = BenchmarkHelpers.TryResolveModelDirectory();
        if (modelDir is null) return;

        try
        {
            _tokenizer = new Tokenizer(modelDir);
        }
        catch (Exception)
        {
            _tokenizer = null;
        }
    }

    /// <summary>Tokenize a short text (~3 tokens). Measures per-call allocation baseline.</summary>
    [Benchmark]
    public long[] Tokenize_ShortText()
    {
        if (_tokenizer is null) return [];
        var (inputIds, _) = _tokenizer.Tokenize(_shortText);
        return inputIds;
    }

    /// <summary>Tokenize a long text (~512 tokens, truncated). Measures full-length tokenization cost.</summary>
    [Benchmark]
    public long[] Tokenize_LongText()
    {
        if (_tokenizer is null) return [];
        var (inputIds, _) = _tokenizer.Tokenize(_longText);
        return inputIds;
    }
}
