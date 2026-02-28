namespace ElBruno.LocalEmbeddings.ImageEmbeddings.Tests;

/// <summary>
/// Tests for SEC-009: ClipTokenizer guards against oversized vocabulary files (>50MB).
/// </summary>
public class ClipTokenizerFileSizeTests
{
    private const long FiftyOneMegabytes = 51L * 1024 * 1024;

    // -------------------------------------------------------------------------
    // SEC-009: vocab file larger than 50MB must be rejected immediately
    // -------------------------------------------------------------------------

    [Fact]
    public void ClipTokenizer_OversizedVocabFile_ThrowsInvalidOperationException()
    {
        var vocabPath = Path.Combine(Path.GetTempPath(), $"vocab_large_{Guid.NewGuid()}.json");
        var mergesPath = Path.Combine(Path.GetTempPath(), $"merges_{Guid.NewGuid()}.txt");

        try
        {
            // Use SetLength to create a sparse 51MB file — avoids actually writing 51MB of data.
            using (var fs = new FileStream(vocabPath, FileMode.Create, FileAccess.Write))
            {
                fs.SetLength(FiftyOneMegabytes);
            }

            // Create a minimal merges file so the path-existence check passes.
            File.WriteAllText(mergesPath, "#version: 0.2\n");

            // SEC-009: the size guard must fire before attempting to read/parse the file.
            Assert.Throws<InvalidOperationException>(() => new ClipTokenizer(vocabPath, mergesPath));
        }
        finally
        {
            if (File.Exists(vocabPath)) File.Delete(vocabPath);
            if (File.Exists(mergesPath)) File.Delete(mergesPath);
        }
    }

    // -------------------------------------------------------------------------
    // SEC-009: vocab file well under 50MB must not trigger the size guard
    // -------------------------------------------------------------------------

    [Fact]
    public void ClipTokenizer_ValidSizeVocabFile_DoesNotThrowOnSizeCheck()
    {
        var vocabPath = Path.Combine(Path.GetTempPath(), $"vocab_small_{Guid.NewGuid()}.json");
        var mergesPath = Path.Combine(Path.GetTempPath(), $"merges_{Guid.NewGuid()}.txt");

        try
        {
            // A tiny valid-JSON vocabulary (well under 50MB).
            File.WriteAllText(vocabPath, "{\"hello\": 1, \"world\": 2}");
            File.WriteAllText(mergesPath, "#version: 0.2\n");

            var ex = Record.Exception(() => new ClipTokenizer(vocabPath, mergesPath));

            // The size guard must NOT fire for a small file.
            // Another exception (e.g., parse error or FileNotFoundException) is acceptable,
            // but the specific SEC-009 size-guard exception must not be raised.
            if (ex != null)
            {
                Assert.False(
                    ex is InvalidOperationException ioe &&
                        ioe.Message.Contains("50MB", StringComparison.OrdinalIgnoreCase),
                    $"Size guard must not fire for a small vocab file, but got: {ex.Message}");
            }
        }
        finally
        {
            if (File.Exists(vocabPath)) File.Delete(vocabPath);
            if (File.Exists(mergesPath)) File.Delete(mergesPath);
        }
    }
}
