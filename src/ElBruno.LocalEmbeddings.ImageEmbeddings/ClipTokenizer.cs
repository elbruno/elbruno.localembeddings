using System.Text.Json;

namespace ElBruno.LocalEmbeddings.ImageEmbeddings;

/// <summary>
/// Minimal tokenizer for CLIP text encoding using BPE.
/// </summary>
/// <remarks>
/// Handles vocabulary loading, special tokens (SOT/EOT), and padding/truncation
/// to CLIP's context length of 77 tokens.
/// </remarks>
public sealed class ClipTokenizer
{
    private readonly Dictionary<string, int> _vocabulary;
    private const int SOT = 49406;  // Start of text token
    private const int EOT = 49407;  // End of text token
    private const int ContextLength = 77;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClipTokenizer"/> class.
    /// </summary>
    /// <param name="vocabJsonPath">Path to the vocabulary JSON file.</param>
    /// <param name="mergesTxtPath">Path to the BPE merge rules file.</param>
    /// <exception cref="InvalidOperationException">Thrown when the vocabulary file cannot be parsed.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the merge rules file is not found.</exception>
    public ClipTokenizer(string vocabJsonPath, string mergesTxtPath)
    {
        var json = File.ReadAllText(vocabJsonPath);
        _vocabulary = JsonSerializer.Deserialize<Dictionary<string, int>>(json)
            ?? throw new InvalidOperationException("Failed to parse vocabulary");

        if (!File.Exists(mergesTxtPath))
        {
            throw new FileNotFoundException($"Merge rules file not found: {mergesTxtPath}");
        }
    }

    /// <summary>
    /// Encodes text into token IDs and attention mask for CLIP text encoding.
    /// </summary>
    /// <param name="text">The text to encode.</param>
    /// <returns>A tuple containing the token IDs and attention mask arrays, each of length 77.</returns>
    /// <remarks>
    /// This is a simplified tokenizer implementation for demonstration purposes.
    /// CLIP's BPE vocabulary is case-sensitive. This implementation converts to lowercase
    /// which may reduce search quality for proper nouns and capitalized words.
    /// For production use, consider using a full BPE tokenizer implementation.
    /// </remarks>
    public (int[] InputIds, int[] AttentionMask) Encode(string text)
    {
        var tokens = new List<int> { SOT };

        var words = text.ToLowerInvariant().Split(
            [' ', '\t', '\n', '\r', '.', ',', '!', '?', ';', ':', '-', '(', ')'],
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in words)
        {
            if (_vocabulary.TryGetValue(word, out int tokenId))
            {
                tokens.Add(tokenId);
            }
            else
            {
                foreach (char c in word)
                {
                    var charStr = c.ToString();
                    if (_vocabulary.TryGetValue(charStr, out int charId))
                    {
                        tokens.Add(charId);
                    }
                }
            }
        }

        tokens.Add(EOT);

        var inputIds = new int[ContextLength];
        var attentionMask = new int[ContextLength];

        int copyLen = Math.Min(tokens.Count, ContextLength);
        for (int i = 0; i < copyLen; i++)
        {
            inputIds[i] = tokens[i];
            attentionMask[i] = 1;
        }

        return (inputIds, attentionMask);
    }
}
