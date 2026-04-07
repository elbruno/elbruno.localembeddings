using Microsoft.ML.Tokenizers;

namespace ElBruno.LocalEmbeddings.Npu.Intel;

/// <summary>
/// Internal tokenizer for the Intel NPU library.
/// Provides BERT-compatible tokenization for sentence-transformer models.
/// </summary>
/// <remarks>
/// This is a standalone copy to avoid referencing the base ElBruno.LocalEmbeddings
/// library, which would cause OnnxRuntime version conflicts with Intel OpenVINO.
/// </remarks>
internal sealed class IntelTokenizer
{
    private readonly BertTokenizer _tokenizer;
    private readonly int _maxLength;

    public int PadTokenId => _tokenizer.PaddingTokenId;
    public int ClsTokenId => _tokenizer.ClassificationTokenId;
    public int SepTokenId => _tokenizer.SeparatorTokenId;
    public int MaxLength => _maxLength;

    public IntelTokenizer(string tokenizerPath, int maxLength = 512)
    {
        if (string.IsNullOrWhiteSpace(tokenizerPath))
        {
            throw new ArgumentException("Tokenizer path cannot be null or empty.", nameof(tokenizerPath));
        }

        var actualPath = Directory.Exists(tokenizerPath)
            ? Path.Combine(tokenizerPath, "vocab.txt")
            : tokenizerPath;

        if (!File.Exists(actualPath))
        {
            throw new FileNotFoundException("Vocab file not found.", actualPath);
        }

        if (maxLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength), "Max length must be positive.");
        }

        _maxLength = maxLength;

        using var stream = File.OpenRead(actualPath);
        _tokenizer = BertTokenizer.Create(stream);
    }

    public (long[] InputIds, long[] AttentionMask) Tokenize(string text, int? maxLength = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        var effectiveMaxLength = maxLength ?? _maxLength;
        if (effectiveMaxLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength), "Max length must be positive.");
        }

        var encoding = _tokenizer.EncodeToIds(text, effectiveMaxLength, out _, out _);

        var inputIds = new long[effectiveMaxLength];
        var attentionMask = new long[effectiveMaxLength];

        var copyLength = Math.Min(encoding.Count, effectiveMaxLength);
        for (int i = 0; i < copyLength; i++)
        {
            inputIds[i] = encoding[i];
            attentionMask[i] = 1;
        }

        return (inputIds, attentionMask);
    }

    public (long[][] InputIds, long[][] AttentionMasks) TokenizeBatch(
        IEnumerable<string> texts,
        int? maxLength,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(texts);

        IList<string> textList = texts as IList<string> ?? texts.ToList();
        if (textList.Count == 0)
        {
            return ([], []);
        }

        var inputIds = new long[textList.Count][];
        var attentionMasks = new long[textList.Count][];

        for (int i = 0; i < textList.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (ids, mask) = Tokenize(textList[i], maxLength);
            inputIds[i] = ids;
            attentionMasks[i] = mask;
        }

        return (inputIds, attentionMasks);
    }

    public int CountTokens(string text, int? maxLength = null)
    {
        var (_, attentionMask) = Tokenize(text, maxLength);
        var count = 0;
        for (int i = 0; i < attentionMask.Length; i++)
        {
            count += (int)attentionMask[i];
        }

        return count;
    }
}
