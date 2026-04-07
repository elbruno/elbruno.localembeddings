using System.Text;
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

    /// <summary>
    /// Gets the maximum sequence length this tokenizer was configured with.
    /// </summary>
    public int MaxLength => _maxLength;

    /// <summary>
    /// Gets the instruction prefix prepended to input text, if any.
    /// </summary>
    public string? InstructionPrefix => _instructionPrefix;

    private HarrierTokenizer(BpeTokenizer tokenizer, int maxLength, string? instructionPrefix)
    {
        _tokenizer = tokenizer;
        _maxLength = maxLength;
        _instructionPrefix = instructionPrefix;
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

        if (maxLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength), "Max length must be positive.");
        }

        var bpeTokenizer = LoadFromTokenizerJson(actualPath);
        return new HarrierTokenizer(bpeTokenizer, maxLength, instructionPrefix);
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
        if (effectiveMaxLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength), "Max length must be positive.");
        }

        // Prepend instruction prefix if configured
        var inputText = !string.IsNullOrEmpty(_instructionPrefix)
            ? _instructionPrefix + text
            : text;

        // Reserve 2 slots for BOS and EOS
        int contentMaxLength = effectiveMaxLength - 2;
        if (contentMaxLength <= 0)
        {
            contentMaxLength = 1;
        }

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
        var (_, attentionMask) = Tokenize(text, maxLength);
        var count = 0;
        for (int i = 0; i < attentionMask.Length; i++)
        {
            count += (int)attentionMask[i];
        }

        return count;
    }

    /// <summary>
    /// Parses tokenizer.json and creates a BpeTokenizer with the extracted vocab and merges.
    /// </summary>
    private static BpeTokenizer LoadFromTokenizerJson(string path)
    {
        using var fileStream = File.OpenRead(path);
        using var doc = JsonDocument.Parse(fileStream, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });

        var root = doc.RootElement;

        if (!root.TryGetProperty("model", out var model))
        {
            throw new InvalidOperationException("tokenizer.json missing 'model' section.");
        }

        // Extract vocab: { "token": id, ... } → write as JSON stream
        if (!model.TryGetProperty("vocab", out var vocabElement))
        {
            throw new InvalidOperationException("tokenizer.json model section missing 'vocab'.");
        }

        // Extract merges: [["a", "b"], ...] or ["a b", ...] → write as text stream
        if (!model.TryGetProperty("merges", out var mergesElement))
        {
            throw new InvalidOperationException("tokenizer.json model section missing 'merges'.");
        }

        // Build vocab JSON stream
        using var vocabStream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(vocabStream))
        {
            vocabElement.WriteTo(writer);
        }
        vocabStream.Position = 0;

        // Build merges text stream
        using var mergesStream = new MemoryStream();
        using (var writer = new StreamWriter(mergesStream, Encoding.UTF8, leaveOpen: true))
        {
            foreach (var merge in mergesElement.EnumerateArray())
            {
                if (merge.ValueKind == JsonValueKind.Array)
                {
                    // Format: ["token1", "token2"]
                    var parts = new string[2];
                    int idx = 0;
                    foreach (var part in merge.EnumerateArray())
                    {
                        if (idx < 2)
                        {
                            parts[idx++] = part.GetString() ?? "";
                        }
                    }

                    writer.Write(parts[0]);
                    writer.Write(' ');
                    writer.WriteLine(parts[1]);
                }
                else if (merge.ValueKind == JsonValueKind.String)
                {
                    // Format: "token1 token2"
                    writer.WriteLine(merge.GetString());
                }
            }

            writer.Flush();
        }
        mergesStream.Position = 0;

        return BpeTokenizer.Create(vocabStream, mergesStream);
    }
}
