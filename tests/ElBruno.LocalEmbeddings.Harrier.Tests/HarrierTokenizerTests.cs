namespace ElBruno.LocalEmbeddings.Harrier.Tests;

public class HarrierTokenizerTests
{
    [Fact]
    public void Create_ThrowsOnNullPath()
    {
        Assert.Throws<ArgumentException>(() => HarrierTokenizer.Create(""));
    }

    [Fact]
    public void Create_ThrowsOnEmptyPath()
    {
        Assert.Throws<ArgumentException>(() => HarrierTokenizer.Create("   "));
    }

    [Fact]
    public void Create_ThrowsOnMissingFile()
    {
        Assert.Throws<FileNotFoundException>(
            () => HarrierTokenizer.Create("/nonexistent/tokenizer.json"));
    }

    [Fact]
    public void Create_ThrowsOnInvalidMaxLength()
    {
        // Use a real temp file to get past the file existence check
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "tokenizer.json"), "{}");
        try
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => HarrierTokenizer.Create(tempDir, maxLength: 0));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Create_ThrowsOnNegativeMaxLength()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "tokenizer.json"), "{}");
        try
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => HarrierTokenizer.Create(tempDir, maxLength: -1));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Create_ThrowsOnMaxLengthLessThan3()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "tokenizer.json"), "{}");
        try
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(
                () => HarrierTokenizer.Create(tempDir, maxLength: 2));
            Assert.Contains("MaxLength must be at least 3", ex.Message);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Create_ThrowsOnMaxLengthOf2()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "tokenizer.json"), "{}");
        try
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => HarrierTokenizer.Create(tempDir, maxLength: 2));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Create_ThrowsOnMaxLengthOf1()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "tokenizer.json"), "{}");
        try
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => HarrierTokenizer.Create(tempDir, maxLength: 1));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
