using ElBruno.LocalEmbeddings.ImageEmbeddings.Options;

namespace ElBruno.LocalEmbeddings.ImageEmbeddings.Tests;

/// <summary>
/// Tests for SEC-003: path traversal and invalid character validation in
/// <see cref="ImageEmbeddingsOptions"/> file-name properties.
/// </summary>
public class ImageEmbeddingsOptionsValidationTests
{
    // -------------------------------------------------------------------------
    // SEC-003: Path traversal — VisionModelFileName
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("../evil.onnx")]
    [InlineData("../../etc/passwd")]
    [InlineData("sub/../../evil.onnx")]
    [InlineData("..evil")]   // contains ".." sequence
    public void VisionModelFileName_PathTraversal_ThrowsArgumentException(string badName)
    {
        var options = new ImageEmbeddingsOptions();

        Assert.Throws<ArgumentException>(() => options.VisionModelFileName = badName);
    }

    // -------------------------------------------------------------------------
    // SEC-003: Path traversal — TextModelFileName
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("../evil.onnx")]
    [InlineData("../../etc/passwd")]
    [InlineData("..\\evil.onnx")]   // backslash traversal sequence
    public void TextModelFileName_PathTraversal_ThrowsArgumentException(string badName)
    {
        var options = new ImageEmbeddingsOptions();

        Assert.Throws<ArgumentException>(() => options.TextModelFileName = badName);
    }

    // -------------------------------------------------------------------------
    // SEC-003: Path traversal — VocabFileName and MergesFileName
    // -------------------------------------------------------------------------

    [Fact]
    public void VocabFileName_PathTraversal_ThrowsArgumentException()
    {
        var options = new ImageEmbeddingsOptions();

        Assert.Throws<ArgumentException>(() => options.VocabFileName = "../vocab.json");
    }

    [Fact]
    public void MergesFileName_PathTraversal_ThrowsArgumentException()
    {
        var options = new ImageEmbeddingsOptions();

        Assert.Throws<ArgumentException>(() => options.MergesFileName = "../merges.txt");
    }

    // -------------------------------------------------------------------------
    // SEC-003: Invalid file-name characters
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("model<1>.onnx")]   // '<' and '>' are invalid on Windows
    [InlineData("model|pipe.onnx")] // '|' is invalid
    [InlineData("model?.onnx")]     // '?' is invalid
    [InlineData("model*.onnx")]     // '*' is invalid
    public void FileName_InvalidChars_ThrowsArgumentException(string badName)
    {
        var options = new ImageEmbeddingsOptions();

        // Invalid characters are detected in all four file-name properties;
        // testing via VisionModelFileName is representative.
        Assert.Throws<ArgumentException>(() => options.VisionModelFileName = badName);
    }

    // -------------------------------------------------------------------------
    // SEC-003: Null / empty / whitespace file names
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FileName_NullOrWhiteSpace_ThrowsArgumentException(string? badName)
    {
        var options = new ImageEmbeddingsOptions();

        Assert.ThrowsAny<ArgumentException>(() => options.TextModelFileName = badName!);
    }

    // -------------------------------------------------------------------------
    // SEC-003: Valid file names — must NOT throw
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("model.onnx")]
    [InlineData("my_model-v2.onnx")]
    [InlineData("clip_vision.onnx")]
    [InlineData("vocab.json")]
    [InlineData("merges.txt")]
    public void FileName_ValidName_DoesNotThrow(string validName)
    {
        var options = new ImageEmbeddingsOptions();

        var ex = Record.Exception(() => options.VisionModelFileName = validName);

        Assert.Null(ex);
    }
}
