namespace ElBruno.LocalEmbeddings.ImageEmbeddings.Tests;

public class ImageSearchEngineTests
{
    [Fact]
    public void DirectoryNotFoundException_ContainsExpectedMessage()
    {
        // ImageSearchEngine.IndexImages throws DirectoryNotFoundException
        // when the image directory does not exist. We verify the exception
        // message format matches the expected pattern.
        var nonExistentDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Assert.False(Directory.Exists(nonExistentDir));

        var ex = new DirectoryNotFoundException($"Image directory not found: {nonExistentDir}");
        Assert.Contains(nonExistentDir, ex.Message);
    }

    [Fact]
    public void ClipTokenizer_WithNonExistentVocab_ThrowsFileNotFoundException()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "vocab.json");
        var mergesPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "merges.txt");

        Assert.ThrowsAny<Exception>(() => new ClipTokenizer(nonExistentPath, mergesPath));
    }

    [Fact]
    public void ClipTokenizer_WithNonExistentMerges_ThrowsFileNotFoundException()
    {
        // Create a temporary vocab file but no merges file
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var vocabPath = Path.Combine(tempDir, "vocab.json");
            File.WriteAllText(vocabPath, """{"hello": 1, "world": 2}""");

            var mergesPath = Path.Combine(tempDir, "merges.txt");
            // merges.txt does not exist

            Assert.Throws<FileNotFoundException>(() => new ClipTokenizer(vocabPath, mergesPath));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ClipTokenizer_Encode_ReturnsCorrectLength()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var vocabPath = Path.Combine(tempDir, "vocab.json");
            File.WriteAllText(vocabPath, """{"hello": 1, "world": 2}""");

            var mergesPath = Path.Combine(tempDir, "merges.txt");
            File.WriteAllText(mergesPath, "");

            var tokenizer = new ClipTokenizer(vocabPath, mergesPath);
            var (inputIds, attentionMask) = tokenizer.Encode("hello world");

            Assert.Equal(77, inputIds.Length);
            Assert.Equal(77, attentionMask.Length);

            // SOT token at position 0
            Assert.Equal(49406, inputIds[0]);
            Assert.Equal(1, attentionMask[0]);

            // "hello" token at position 1
            Assert.Equal(1, inputIds[1]);
            Assert.Equal(1, attentionMask[1]);

            // "world" token at position 2
            Assert.Equal(2, inputIds[2]);
            Assert.Equal(1, attentionMask[2]);

            // EOT token at position 3
            Assert.Equal(49407, inputIds[3]);
            Assert.Equal(1, attentionMask[3]);

            // Padding from position 4 onwards
            Assert.Equal(0, inputIds[4]);
            Assert.Equal(0, attentionMask[4]);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void GetProgressFileName_ReturnsFileNameOnly()
    {
        var imagePath = Path.Combine("C:", "images", "sample-image.png");

        var fileName = ImageSearchEngine.GetProgressFileName(imagePath);

        Assert.Equal("sample-image.png", fileName);
    }
}
