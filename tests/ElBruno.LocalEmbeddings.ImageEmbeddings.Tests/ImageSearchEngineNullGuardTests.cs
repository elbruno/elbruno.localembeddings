namespace ElBruno.LocalEmbeddings.ImageEmbeddings.Tests;

/// <summary>
/// Tests for SEC-005: null guards added to <see cref="ImageSearchEngine"/>
/// constructor and search methods.
/// </summary>
public class ImageSearchEngineNullGuardTests
{
    // -------------------------------------------------------------------------
    // SEC-005: Constructor null guards — no ONNX files needed, guard fires before
    // any encoder is loaded.
    // -------------------------------------------------------------------------

    [Fact]
    public void ImageSearchEngine_NullImageEncoder_ThrowsArgumentNullException()
    {
        // ArgumentNullException.ThrowIfNull(imageEncoder) fires first.
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ImageSearchEngine(null!, null!));

        Assert.Equal("imageEncoder", ex.ParamName);
    }

    [SkippableFact]
    public void ImageSearchEngine_NullTextEncoder_ThrowsArgumentNullException()
    {
        // Reaching the textEncoder null check requires a valid ClipImageEncoder,
        // which in turn requires a real ONNX vision model on disk.
        var visionModelPath = Environment.GetEnvironmentVariable("CLIP_VISION_MODEL_PATH");
        Skip.If(string.IsNullOrEmpty(visionModelPath),
            "Set CLIP_VISION_MODEL_PATH to the CLIP vision ONNX model file to run this test.");

        using var imageEncoder = new ClipImageEncoder(visionModelPath!);

        var ex = Assert.Throws<ArgumentNullException>(() =>
            new ImageSearchEngine(imageEncoder, null!));

        Assert.Equal("textEncoder", ex.ParamName);
    }

    // -------------------------------------------------------------------------
    // SEC-005: SearchByText null / empty / whitespace query guard.
    // The guard fires before any encoder call or index access, so an empty
    // image index is sufficient — but a live engine is still required.
    // -------------------------------------------------------------------------

    [SkippableFact]
    public void SearchByText_NullQuery_ThrowsArgumentException()
    {
        var engine = CreateEngineFromEnvironment();

        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            engine.SearchByText(null!));

        Assert.Equal("query", ex.ParamName);
    }

    [SkippableFact]
    public void SearchByText_EmptyQuery_ThrowsArgumentException()
    {
        var engine = CreateEngineFromEnvironment();

        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            engine.SearchByText(string.Empty));

        Assert.Equal("query", ex.ParamName);
    }

    [SkippableFact]
    public void SearchByText_WhiteSpaceQuery_ThrowsArgumentException()
    {
        var engine = CreateEngineFromEnvironment();

        var ex = Assert.ThrowsAny<ArgumentException>(() =>
            engine.SearchByText("   "));

        Assert.Equal("query", ex.ParamName);
    }

    // -------------------------------------------------------------------------
    // SEC-005: SearchByText with a valid non-empty query and an empty index
    // returns an empty list (guard passes, early-return is reached).
    // -------------------------------------------------------------------------

    [SkippableFact]
    public void SearchByText_ValidQuery_EmptyIndex_ReturnsEmptyList()
    {
        var engine = CreateEngineFromEnvironment();

        var results = engine.SearchByText("a cat on a sofa");

        Assert.Empty(results);
    }

    // -------------------------------------------------------------------------
    // Helper
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates an <see cref="ImageSearchEngine"/> from environment variables.
    /// Skips the calling test if any required variable is absent.
    /// </summary>
    private static ImageSearchEngine CreateEngineFromEnvironment()
    {
        var visionModelPath = Environment.GetEnvironmentVariable("CLIP_VISION_MODEL_PATH");
        var textModelPath = Environment.GetEnvironmentVariable("CLIP_TEXT_MODEL_PATH");
        var vocabPath = Environment.GetEnvironmentVariable("CLIP_VOCAB_PATH");
        var mergesPath = Environment.GetEnvironmentVariable("CLIP_MERGES_PATH");

        Skip.If(
            string.IsNullOrEmpty(visionModelPath)
            || string.IsNullOrEmpty(textModelPath)
            || string.IsNullOrEmpty(vocabPath)
            || string.IsNullOrEmpty(mergesPath),
            "Set CLIP_VISION_MODEL_PATH, CLIP_TEXT_MODEL_PATH, CLIP_VOCAB_PATH, and " +
            "CLIP_MERGES_PATH to run CLIP integration tests.");

        var imageEncoder = new ClipImageEncoder(visionModelPath!);
        var textEncoder = new ClipTextEncoder(textModelPath!, vocabPath!, mergesPath!);
        return new ImageSearchEngine(imageEncoder, textEncoder);
    }
}
