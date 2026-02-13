using Microsoft.ML.Tokenizers;

namespace elbruno.LocalEmbeddings;

/// <summary>
/// Tokenizer for text-to-embedding conversion using HuggingFace tokenizer files.
/// </summary>
/// <remarks>
/// <para>
/// This class wraps Microsoft.ML.Tokenizers to provide tokenization compatible with
/// HuggingFace sentence-transformers models like all-MiniLM.
/// </para>
/// <para>
/// Instances of this class are thread-safe after initialization. Multiple threads
/// can call <see cref="Tokenize"/> concurrently.
/// </para>
/// </remarks>
public sealed class Tokenizer
{
    private readonly BertTokenizer _tokenizer;
    private readonly int _maxLength;

    /// <summary>
    /// Gets the padding token ID used by this tokenizer.
    /// </summary>
    public int PadTokenId => _tokenizer.PaddingTokenId;

    /// <summary>
    /// Gets the CLS (start of sequence) token ID used by this tokenizer.
    /// </summary>
    public int ClsTokenId => _tokenizer.ClassificationTokenId;

    /// <summary>
    /// Gets the SEP (end of sequence) token ID used by this tokenizer.
    /// </summary>
    public int SepTokenId => _tokenizer.SeparatorTokenId;

    /// <summary>
    /// Gets the maximum sequence length this tokenizer was configured with.
    /// </summary>
    public int MaxLength => _maxLength;

    /// <summary>
    /// Creates a new tokenizer from a tokenizer.json file.
    /// </summary>
    /// <param name="tokenizerPath">Path to the vocab.txt file or model directory containing it.</param>
    /// <param name="maxLength">Maximum sequence length (default: 512).</param>
    /// <exception cref="ArgumentException">Thrown when the path is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the tokenizer file is not found.</exception>
    public Tokenizer(string tokenizerPath, int maxLength = 512)
    {
        if (string.IsNullOrWhiteSpace(tokenizerPath))
        {
            throw new ArgumentException("Tokenizer path cannot be null or empty.", nameof(tokenizerPath));
        }

        // If a directory is provided, look for vocab.txt inside
        // BertTokenizer.Create expects a vocab.txt file (one token per line), not tokenizer.json
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

        // Load using the vocab.txt file (BertTokenizer format)
        using var stream = File.OpenRead(actualPath);
        _tokenizer = BertTokenizer.Create(stream);
    }

    /// <summary>
    /// Tokenizes the input text and returns input IDs and attention mask.
    /// </summary>
    /// <param name="text">The text to tokenize.</param>
    /// <param name="maxLength">Optional override for maximum sequence length. Uses the instance default if not specified.</param>
    /// <returns>A tuple containing the input IDs and attention mask arrays.</returns>
    /// <exception cref="ArgumentNullException">Thrown when text is null.</exception>
    /// <remarks>
    /// <para>
    /// The output includes special tokens (CLS at start, SEP at end) and is padded/truncated
    /// to the specified maximum length.
    /// </para>
    /// <para>
    /// This method is thread-safe.
    /// </para>
    /// </remarks>
    public (long[] InputIds, long[] AttentionMask) Tokenize(string text, int? maxLength = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        var effectiveMaxLength = maxLength ?? _maxLength;
        if (effectiveMaxLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength), "Max length must be positive.");
        }

        // Encode the text with special tokens
        var encoding = _tokenizer.EncodeToIds(text, effectiveMaxLength, out _, out _);
        var tokenIds = encoding.ToArray();

        // Create arrays for input_ids and attention_mask
        var inputIds = new long[effectiveMaxLength];
        var attentionMask = new long[effectiveMaxLength];

        // Copy token IDs and set attention mask
        var copyLength = Math.Min(tokenIds.Length, effectiveMaxLength);
        for (int i = 0; i < copyLength; i++)
        {
            inputIds[i] = tokenIds[i];
            attentionMask[i] = 1;
        }

        // Remaining positions are already 0 (padding) by default

        return (inputIds, attentionMask);
    }

    /// <summary>
    /// Tokenizes multiple texts, padding all to the same length for batched inference.
    /// </summary>
    /// <param name="texts">The texts to tokenize.</param>
    /// <param name="maxLength">Optional override for maximum sequence length.</param>
    /// <returns>A tuple containing arrays of input IDs and attention masks.</returns>
    /// <exception cref="ArgumentNullException">Thrown when texts is null.</exception>
    /// <remarks>
    /// <para>
    /// All outputs are padded to the same length (the specified maxLength), making them
    /// suitable for batched inference with <see cref="OnnxEmbeddingModel.GenerateEmbeddings"/>.
    /// </para>
    /// <para>
    /// This method is thread-safe.
    /// </para>
    /// </remarks>
    public (long[][] InputIds, long[][] AttentionMasks) TokenizeBatch(IEnumerable<string> texts, int? maxLength = null)
    {
        ArgumentNullException.ThrowIfNull(texts);

        var textList = texts.ToList();
        if (textList.Count == 0)
        {
            return ([], []);
        }

        var inputIds = new long[textList.Count][];
        var attentionMasks = new long[textList.Count][];

        for (int i = 0; i < textList.Count; i++)
        {
            var (ids, mask) = Tokenize(textList[i], maxLength);
            inputIds[i] = ids;
            attentionMasks[i] = mask;
        }

        return (inputIds, attentionMasks);
    }
}
