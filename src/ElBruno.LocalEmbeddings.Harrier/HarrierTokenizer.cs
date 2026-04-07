using System.Text.Json;
using Microsoft.ML.Tokenizers;

namespace ElBruno.LocalEmbeddings.Harrier;

/// <summary>
/// Tokenizer for Harrier embedding models that loads from <c>tokenizer.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// The Harrier model uses a Gemma 3 BPE tokenizer with SentencePiece conventions.
/// This class parses the HuggingFace <c>tokenizer.json</c> format, extracts the vocabulary
/// and merge rules, and creates a <see cref="BpeTokenizer"/> for encoding.
/// </para>
/// <para>
/// Special tokens: BOS (id=2) is prepended and EOS (id=1) is appended to each sequence,
/// matching the post-processor defined in the tokenizer configuration.
/// </para>
/// <para>
/// Instances of this class are thread-safe after initialization.
/// </para>
/// </remarks>
public sealed class HarrierTokenizer
{
    private const int BosTokenId = 2;
    private const int EosTokenId = 1;
    private const int PadTokenId = 0;

    private readonly BpeTokenizer _tokenizer;
    private readonly int _maxLength;
    private readonly string? _instructionPrefix;
    private readonly bool _tokenizerIndicatesSentencePieceNormalization;

    /// <summary>
    /// Gets the maximum sequence length this tokenizer was configured with.
    /// </summary>
    public int MaxLength => _maxLength;

    /// <summary>
    /// Gets the instruction prefix prepended to input text, if any.
    /// </summary>
    public string? InstructionPrefix => _instructionPrefix;

    private HarrierTokenizer(
        BpeTokenizer tokenizer,
        int maxLength,
        string? instructionPrefix,
        bool tokenizerIndicatesSentencePieceNormalization)
    {
        _tokenizer = tokenizer;
        _maxLength = maxLength;
        _instructionPrefix = instructionPrefix;
        _tokenizerIndicatesSentencePieceNormalization = tokenizerIndicatesSentencePieceNormalization;
    }

    /// <summary>
    /// Creates a Harrier tokenizer from a <c>tokenizer.json</c> file.
    /// </summary>
    /// <param name="tokenizerPath">Path to the <c>tokenizer.json</c> file or model directory containing it.</param>
    /// <param name="maxLength">Maximum sequence length (default: 8192).</param>
    /// <param name="instructionPrefix">Optional instruction prefix to prepend to input text.</param>
    /// <exception cref="ArgumentException">Thrown when the path is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the tokenizer.json file is not found.</exception>
    public static HarrierTokenizer Create(string tokenizerPath, int maxLength = 8192, string? instructionPrefix = null)
    {
        if (string.IsNullOrWhiteSpace(tokenizerPath))
        {
            throw new ArgumentException("Tokenizer path cannot be null or empty.", nameof(tokenizerPath));
        }

        var actualPath = Directory.Exists(tokenizerPath)
            ? Path.Combine(tokenizerPath, "tokenizer.json")
            : tokenizerPath;

        if (!File.Exists(actualPath))
        {
            throw new FileNotFoundException("tokenizer.json file not found.", actualPath);
        }

        if (maxLength < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength), maxLength, "MaxLength must be at least 3 (BOS + 1 token + EOS).");
        }

        var bpeTokenizer = LoadFromTokenizerJson(actualPath, out var tokenizerIndicatesSentencePieceNormalization);
        return new HarrierTokenizer(
            bpeTokenizer,
            maxLength,
            instructionPrefix,
            tokenizerIndicatesSentencePieceNormalization);
    }

    /// <summary>
    /// Tokenizes the input text with optional instruction prefix and returns input IDs and attention mask.
    /// </summary>
    /// <remarks>
    /// The output includes BOS token at start and EOS token at end, padded/truncated
    /// to the configured maximum length.
    /// </remarks>
    public (long[] InputIds, long[] AttentionMask) Tokenize(string text, int? maxLength = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        var effectiveMaxLength = maxLength ?? _maxLength;
        if (effectiveMaxLength < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength), effectiveMaxLength, "MaxLength must be at least 3 (BOS + 1 token + EOS).");
        }

        // Prepend instruction prefix if configured
        var inputText = !string.IsNullOrEmpty(_instructionPrefix)
            ? _instructionPrefix + text
            : text;

        if (!_tokenizerIndicatesSentencePieceNormalization)
        {
            // tokenizer.json may omit the normalizer section, but Gemma tokenizers still require SentencePiece whitespace handling.
        }

        // Apply SentencePiece normalization: prepend ▁ and replace spaces with ▁
        inputText = "\u2581" + inputText.Replace(' ', '\u2581');

        // Reserve 2 slots for BOS and EOS
        int contentMaxLength = effectiveMaxLength - 2;
        contentMaxLength = Math.Max(1, contentMaxLength);

        // Encode the text (without special tokens)
        var encoding = _tokenizer.EncodeToIds(inputText, contentMaxLength, out _, out _);

        var inputIds = new long[effectiveMaxLength];
        var attentionMask = new long[effectiveMaxLength];

        // BOS token
        inputIds[0] = BosTokenId;
        attentionMask[0] = 1;

        // Content tokens
        var copyLength = Math.Min(encoding.Count, contentMaxLength);
        for (int i = 0; i < copyLength; i++)
        {
            inputIds[i + 1] = encoding[i];
            attentionMask[i + 1] = 1;
        }

        // EOS token
        int eosPosition = copyLength + 1;
        if (eosPosition < effectiveMaxLength)
        {
            inputIds[eosPosition] = EosTokenId;
            attentionMask[eosPosition] = 1;
        }

        // Remaining positions are 0 (PAD) by default

        return (inputIds, attentionMask);
    }

    /// <summary>
    /// Tokenizes multiple texts, padding all to the same length for batched inference.
    /// </summary>
    public (long[][] InputIds, long[][] AttentionMasks) TokenizeBatch(IEnumerable<string> texts, int? maxLength = null)
        => TokenizeBatch(texts, maxLength, CancellationToken.None);

    /// <summary>
    /// Tokenizes multiple texts, padding all to the same length for batched inference.
    /// </summary>
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

    /// <summary>
    /// Counts tokens for the specified text (including instruction prefix and special tokens).
    /// </summary>
    public int CountTokens(string text, int? maxLength = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        var effectiveMaxLength = maxLength ?? _maxLength;
        if (effectiveMaxLength < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength), effectiveMaxLength, "MaxLength must be at least 3 (BOS + 1 token + EOS).");
        }
        var contentMaxLength = Math.Max(1, effectiveMaxLength - 2);

        var inputText = !string.IsNullOrEmpty(_instructionPrefix)
            ? _instructionPrefix + text
            : text;

        // Apply SentencePiece normalization
        inputText = "\u2581" + inputText.Replace(' ', '\u2581');

        var encoding = _tokenizer.EncodeToIds(inputText, contentMaxLength, out _, out _);
        return encoding.Count + 2;
    }

    /// <summary>
    /// Parses tokenizer.json and creates a BpeTokenizer with the extracted vocab and merges.
    /// </summary>
    private static BpeTokenizer LoadFromTokenizerJson(string path, out bool tokenizerIndicatesSentencePieceNormalization)
    {
        const long MaxTokenizerFileSizeBytes = 100 * 1024 * 1024;
        var fileInfo = new FileInfo(path);
        if (fileInfo.Length > MaxTokenizerFileSizeBytes)
        {
            throw new InvalidOperationException(
                $"Tokenizer file exceeds maximum allowed size of {MaxTokenizerFileSizeBytes / (1024 * 1024)} MB: {path} ({fileInfo.Length / (1024 * 1024)} MB)");
        }

        using var fileStream = File.OpenRead(path);
        using var doc = JsonDocument.Parse(fileStream, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });

        var root = doc.RootElement;
        tokenizerIndicatesSentencePieceNormalization = TokenizerIndicatesSentencePieceNormalization(root);

        if (!root.TryGetProperty("model", out var model))
        {
            throw new InvalidOperationException("tokenizer.json missing 'model' section.");
        }

        if (!model.TryGetProperty("vocab", out var vocabElement))
        {
            throw new InvalidOperationException("tokenizer.json model section missing 'vocab'.");
        }

        if (!model.TryGetProperty("merges", out var mergesElement))
        {
            throw new InvalidOperationException("tokenizer.json model section missing 'merges'.");
        }

        // Build vocabulary directly from JSON.
        var vocabulary = new List<KeyValuePair<string, int>>();
        foreach (var property in vocabElement.EnumerateObject())
        {
            vocabulary.Add(new KeyValuePair<string, int>(property.Name, property.Value.GetInt32()));
        }

        // Build merges using BpeOptions instead of a text stream.
        //
        // BpeTokenizer.Create(Stream vocabStream, Stream mergesStream) reads the merges stream
        // line-by-line and fails when a token piece contains embedded newline characters (e.g.
        // the Harrier/Gemma tokenizer has merge entries whose pieces are raw '\n' or '\t' bytes).
        // BpeOptions.Merges accepts an IEnumerable<string> where each element is "piece1 piece2",
        // split internally on the first 0x20 space — this works because Harrier tokens use ▁
        // (U+2581) for word boundaries instead of 0x20, so embedded newlines are not a problem.
        var merges = new List<string>(mergesElement.GetArrayLength());
        foreach (var merge in mergesElement.EnumerateArray())
        {
            if (merge.ValueKind == JsonValueKind.Array)
            {
                string? part0 = null, part1 = null;
                int idx = 0;
                foreach (var part in merge.EnumerateArray())
                {
                    if (idx == 0) part0 = part.GetString();
                    else if (idx == 1) part1 = part.GetString();
                    idx++;
                }

                if (part0 is not null && part1 is not null)
                {
                    merges.Add($"{part0} {part1}");
                }
            }
            else if (merge.ValueKind == JsonValueKind.String)
            {
                var s = merge.GetString();
                if (s is not null)
                {
                    merges.Add(s);
                }
            }
        }

        // Read optional model-level BPE settings from tokenizer.json.
        string? unknownToken = model.TryGetProperty("unk_token", out var unkEl) && unkEl.ValueKind == JsonValueKind.String
            ? unkEl.GetString()
            : null;
        bool fuseUnknown = model.TryGetProperty("fuse_unk", out var fuseEl) && fuseEl.ValueKind == JsonValueKind.True;
        string continuingSubwordPrefix = model.TryGetProperty("continuing_subword_prefix", out var prefixEl) && prefixEl.ValueKind == JsonValueKind.String
            ? prefixEl.GetString() ?? string.Empty
            : string.Empty;
        string endOfWordSuffix = model.TryGetProperty("end_of_word_suffix", out var suffixEl) && suffixEl.ValueKind == JsonValueKind.String
            ? suffixEl.GetString() ?? string.Empty
            : string.Empty;

        var options = new BpeOptions(vocabulary)
        {
            Merges = merges,
            UnknownToken = unknownToken,
            FuseUnknownTokens = fuseUnknown,
            ContinuingSubwordPrefix = continuingSubwordPrefix,
            EndOfWordSuffix = endOfWordSuffix,
        };

        return BpeTokenizer.Create(options);
    }

    private static bool TokenizerIndicatesSentencePieceNormalization(JsonElement root)
    {
        var hasNormalizer = root.TryGetProperty("normalizer", out _);
        if (hasNormalizer)
        {
            return true;
        }

        return root.TryGetProperty("pre_tokenizer", out var preTokenizer)
               && JsonElementContainsString(preTokenizer, "Metaspace");
    }

    private static bool JsonElementContainsString(JsonElement element, string value)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return string.Equals(element.GetString(), value, StringComparison.OrdinalIgnoreCase);
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (JsonElementContainsString(item, value))
                    {
                        return true;
                    }
                }

                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (JsonElementContainsString(property.Value, value))
                    {
                        return true;
                    }
                }

                break;
        }

        return false;
    }
}
