namespace ElBruno.LocalEmbeddings.ImageEmbeddings.Tests;

/// <summary>
/// Tests for SEC-004: null and file-existence guards added to the
/// <see cref="ClipImageEncoder"/> and <see cref="ClipTextEncoder"/> constructors.
/// </summary>
public class ClipEncoderConstructorTests
{
    // -------------------------------------------------------------------------
    // ClipImageEncoder — null / whitespace path
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ClipImageEncoder_NullOrWhiteSpacePath_ThrowsArgumentException(string? path)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ClipImageEncoder(path!));
    }

    // -------------------------------------------------------------------------
    // ClipImageEncoder — non-existent file
    // -------------------------------------------------------------------------

    [Fact]
    public void ClipImageEncoder_NonExistentFile_ThrowsFileNotFoundException()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "vision_model.onnx");

        Assert.Throws<FileNotFoundException>(() => new ClipImageEncoder(nonExistentPath));
    }

    // -------------------------------------------------------------------------
    // ClipTextEncoder — null / whitespace modelPath
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ClipTextEncoder_NullOrWhiteSpaceModelPath_ThrowsArgumentException(string? modelPath)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ClipTextEncoder(modelPath!, "vocab.json", "merges.txt"));
    }

    // -------------------------------------------------------------------------
    // ClipTextEncoder — null / whitespace vocabPath
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ClipTextEncoder_NullOrEmptyVocabPath_ThrowsArgumentException(string? vocabPath)
    {
        // modelPath validation fires first if it is also invalid, so use a
        // syntactically non-empty model path to reach the vocabPath check.
        Assert.ThrowsAny<ArgumentException>(() =>
            new ClipTextEncoder("text_model.onnx", vocabPath!, "merges.txt"));
    }

    // -------------------------------------------------------------------------
    // ClipTextEncoder — null / whitespace mergesPath
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ClipTextEncoder_NullOrEmptyMergesPath_ThrowsArgumentException(string? mergesPath)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new ClipTextEncoder("text_model.onnx", "vocab.json", mergesPath!));
    }

    // -------------------------------------------------------------------------
    // ClipTextEncoder — non-existent model file
    // -------------------------------------------------------------------------

    [Fact]
    public void ClipTextEncoder_NonExistentModelFile_ThrowsFileNotFoundException()
    {
        var nonExistentModel = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "text_model.onnx");
        var nonExistentVocab = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "vocab.json");
        var nonExistentMerges = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "merges.txt");

        Assert.Throws<FileNotFoundException>(() =>
            new ClipTextEncoder(nonExistentModel, nonExistentVocab, nonExistentMerges));
    }

    // -------------------------------------------------------------------------
    // ClipTextEncoder — non-existent vocab file (model exists as a real file)
    // -------------------------------------------------------------------------

    [Fact]
    public void ClipTextEncoder_NonExistentVocabFile_ThrowsFileNotFoundException()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            // Create a placeholder model file so the modelPath existence check passes.
            var modelPath = Path.Combine(tempDir, "text_model.onnx");
            File.WriteAllBytes(modelPath, []);

            var nonExistentVocab = Path.Combine(tempDir, "vocab.json");
            var nonExistentMerges = Path.Combine(tempDir, "merges.txt");

            Assert.Throws<FileNotFoundException>(() =>
                new ClipTextEncoder(modelPath, nonExistentVocab, nonExistentMerges));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
