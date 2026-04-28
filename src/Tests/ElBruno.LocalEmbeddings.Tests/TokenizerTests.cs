namespace ElBruno.LocalEmbeddings.Tests;

public class TokenizerTests
{
    [Fact]
    public void Constructor_WithNullOrEmptyPath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Tokenizer(null!));
        Assert.Throws<ArgumentException>(() => new Tokenizer(""));
        Assert.Throws<ArgumentException>(() => new Tokenizer("   "));
    }

    [Fact]
    public void Constructor_WithNonExistentPath_ThrowsFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() => new Tokenizer("/nonexistent/path/tokenizer.json"));
    }

    [Fact]
    public void Constructor_WithInvalidMaxLength_ThrowsArgumentOutOfRangeException()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Tokenizer(tempFile, maxLength: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Tokenizer(tempFile, maxLength: -1));
        }
        finally
        {
            // Clean up - this will fail anyway due to invalid tokenizer content
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public void Tokenize_WithNullText_ThrowsArgumentNullException()
    {
        // We need a real tokenizer for this test
        var tokenizerPath = GetTokenizerPath();
        Skip.If(tokenizerPath == null, "Tokenizer file not available for testing");

        var tokenizer = new Tokenizer(tokenizerPath);
        Assert.Throws<ArgumentNullException>(() => tokenizer.Tokenize(null!));
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public void Tokenize_WithInvalidMaxLength_ThrowsArgumentOutOfRangeException()
    {
        var tokenizerPath = GetTokenizerPath();
        Skip.If(tokenizerPath == null, "Tokenizer file not available for testing");

        var tokenizer = new Tokenizer(tokenizerPath);
        Assert.Throws<ArgumentOutOfRangeException>(() => tokenizer.Tokenize("hello", maxLength: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => tokenizer.Tokenize("hello", maxLength: -5));
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public void Tokenize_ProducesExpectedTokenIdsAndAttentionMask()
    {
        var tokenizerPath = GetTokenizerPath();
        Skip.If(tokenizerPath == null, "Tokenizer file not available for testing");

        var tokenizer = new Tokenizer(tokenizerPath, maxLength: 128);
        var (inputIds, attentionMask) = tokenizer.Tokenize("Hello world");

        // Check array lengths match maxLength
        Assert.Equal(128, inputIds.Length);
        Assert.Equal(128, attentionMask.Length);

        // Should have some non-zero tokens at the start
        Assert.NotEqual(0, inputIds[0]); // CLS token
        Assert.Equal(1L, attentionMask[0]);

        // Should have padding (zeros) toward the end for short text
        var hasAtLeastOnePad = inputIds.Skip(10).Any(id => id == tokenizer.PadTokenId);
        Assert.True(hasAtLeastOnePad, "Short text should have padding");

        // Attention mask should be 0 where padding is
        for (int i = 0; i < inputIds.Length; i++)
        {
            if (inputIds[i] == tokenizer.PadTokenId && i > 0)
            {
                Assert.Equal(0L, attentionMask[i]);
            }
        }
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public void Tokenize_TruncatesLongSequences()
    {
        var tokenizerPath = GetTokenizerPath();
        Skip.If(tokenizerPath == null, "Tokenizer file not available for testing");

        var tokenizer = new Tokenizer(tokenizerPath, maxLength: 16);

        // Create a very long text
        var longText = string.Join(" ", Enumerable.Repeat("word", 1000));
        var (inputIds, attentionMask) = tokenizer.Tokenize(longText);

        // Should be truncated to maxLength
        Assert.Equal(16, inputIds.Length);
        Assert.Equal(16, attentionMask.Length);

        // All positions should have attention (no padding for truncated text)
        Assert.All(attentionMask, m => Assert.Equal(1L, m));
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public void Tokenize_WithOverrideMaxLength_UsesOverride()
    {
        var tokenizerPath = GetTokenizerPath();
        Skip.If(tokenizerPath == null, "Tokenizer file not available for testing");

        var tokenizer = new Tokenizer(tokenizerPath, maxLength: 512);
        var (inputIds, attentionMask) = tokenizer.Tokenize("Hello", maxLength: 32);

        Assert.Equal(32, inputIds.Length);
        Assert.Equal(32, attentionMask.Length);
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public void TokenizeBatch_WithEmptyCollection_ReturnsEmptyArrays()
    {
        var tokenizerPath = GetTokenizerPath();
        Skip.If(tokenizerPath == null, "Tokenizer file not available for testing");

        var tokenizer = new Tokenizer(tokenizerPath);
        var (inputIds, attentionMasks) = tokenizer.TokenizeBatch([]);

        Assert.Empty(inputIds);
        Assert.Empty(attentionMasks);
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public void TokenizeBatch_WithNullCollection_ThrowsArgumentNullException()
    {
        var tokenizerPath = GetTokenizerPath();
        Skip.If(tokenizerPath == null, "Tokenizer file not available for testing");

        var tokenizer = new Tokenizer(tokenizerPath);
        Assert.Throws<ArgumentNullException>(() => tokenizer.TokenizeBatch(null!));
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public void TokenizeBatch_PadsAllToSameLength()
    {
        var tokenizerPath = GetTokenizerPath();
        Skip.If(tokenizerPath == null, "Tokenizer file not available for testing");

        var tokenizer = new Tokenizer(tokenizerPath, maxLength: 64);
        var texts = new[]
        {
            "Short",
            "This is a longer sentence with more words",
            "Medium length"
        };

        var (inputIds, attentionMasks) = tokenizer.TokenizeBatch(texts);

        Assert.Equal(3, inputIds.Length);
        Assert.Equal(3, attentionMasks.Length);

        // All sequences should have the same length
        Assert.All(inputIds, ids => Assert.Equal(64, ids.Length));
        Assert.All(attentionMasks, mask => Assert.Equal(64, mask.Length));
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public void TokenizeBatch_ProcessesMultipleInputsCorrectly()
    {
        var tokenizerPath = GetTokenizerPath();
        Skip.If(tokenizerPath == null, "Tokenizer file not available for testing");

        var tokenizer = new Tokenizer(tokenizerPath, maxLength: 64);
        var texts = new[]
        {
            "First sentence",
            "Second sentence",
            "Third sentence"
        };

        var (inputIds, attentionMasks) = tokenizer.TokenizeBatch(texts);

        // Each text should produce different token IDs
        for (int i = 0; i < texts.Length; i++)
        {
            for (int j = i + 1; j < texts.Length; j++)
            {
                // Different texts should (likely) have different token representations
                // At least one token position should differ
                var hasAnyDifference = inputIds[i].Zip(inputIds[j]).Any(pair => pair.First != pair.Second);
                Assert.True(hasAnyDifference, "Different texts should produce different tokens");
            }
        }
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public void MaxLength_ReturnsConfiguredValue()
    {
        var tokenizerPath = GetTokenizerPath();
        Skip.If(tokenizerPath == null, "Tokenizer file not available for testing");

        var tokenizer = new Tokenizer(tokenizerPath, maxLength: 256);
        Assert.Equal(256, tokenizer.MaxLength);
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public void SpecialTokenIds_AreValid()
    {
        var tokenizerPath = GetTokenizerPath();
        Skip.If(tokenizerPath == null, "Tokenizer file not available for testing");

        var tokenizer = new Tokenizer(tokenizerPath);

        // Special token IDs should be non-negative
        Assert.True(tokenizer.PadTokenId >= 0);
        Assert.True(tokenizer.ClsTokenId >= 0);
        Assert.True(tokenizer.SepTokenId >= 0);

        // They should all be different
        var specialTokens = new[] { tokenizer.PadTokenId, tokenizer.ClsTokenId, tokenizer.SepTokenId };
        Assert.Equal(3, specialTokens.Distinct().Count());
    }

    /// <summary>
    /// Gets the path to the model directory containing vocab.txt (for integration tests).
    /// </summary>
    /// <remarks>
    /// Returns the model directory path, not a specific file. The <see cref="Tokenizer"/>
    /// constructor handles directories by looking for <c>vocab.txt</c> inside.
    /// </remarks>
    private static string? GetTokenizerPath()
    {
        // Check default cache location
        var defaultCache = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LocalEmbeddings", "models")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "LocalEmbeddings", "models");

        var modelDir = Path.Combine(defaultCache, "sentence-transformers_all-MiniLM-L6-v2");
        var vocabPath = Path.Combine(modelDir, "vocab.txt");

        if (File.Exists(vocabPath))
            return modelDir;

        // Try alternative locations
        var envPath = Environment.GetEnvironmentVariable("LOCALEMBEDDINGS_TEST_TOKENIZER");
        if (!string.IsNullOrEmpty(envPath) && (Directory.Exists(envPath) || File.Exists(envPath)))
            return envPath;

        return null;
    }
}
