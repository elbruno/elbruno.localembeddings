using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ElBruno.LocalEmbeddings.Tests.Phase2.Helpers;

namespace ElBruno.LocalEmbeddings.Tests.Phase2.Fixtures;

/// <summary>
/// Shared fixture for providing test data across Phase 2 test suites.
/// Handles initialization and cleanup of test data resources (files, directories).
/// Implements IAsyncLifetime for async resource management.
/// </summary>
public class TestDataFixture : IAsyncLifetime
{
    private readonly string _testDataDirectory;
    public List<string> GeneratedFiles { get; } = new();
    public List<string> GeneratedDirectories { get; } = new();

    public TestDataFixture()
    {
        _testDataDirectory = Path.Combine(Path.GetTempPath(), "elbruno-test-data");
    }

    public async Task InitializeAsync()
    {
        // Ensure test data directory exists
        Directory.CreateDirectory(_testDataDirectory);
        GeneratedDirectories.Add(_testDataDirectory);

        // Generate semantic pairs test data file if needed
        await EnsureSemanticPairsFile();

        // Generate batch texts test data file if needed
        await EnsureBatchTextsFile();

        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        // Clean up generated files
        foreach (var file in GeneratedFiles)
        {
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        // Clean up generated directories
        foreach (var dir in GeneratedDirectories)
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        GeneratedFiles.Clear();
        GeneratedDirectories.Clear();

        await Task.CompletedTask;
    }

    public string GetTestDataDirectory() => _testDataDirectory;

    public string GetSemanticPairsPath()
    {
        return Path.Combine(_testDataDirectory, "semantic-pairs.csv");
    }

    public string GetBatchTextsPath()
    {
        return Path.Combine(_testDataDirectory, "batch-texts-1k.txt");
    }

    public string GetEdgeCasesPath()
    {
        return Path.Combine(_testDataDirectory, "edge-cases.txt");
    }

    private async Task EnsureSemanticPairsFile()
    {
        var path = GetSemanticPairsPath();
        if (File.Exists(path))
            return;

        var pairs = EmbeddingDataFactory.GenerateSemanticPairs();
        var lines = new List<string> { "Text1,Text2,ExpectedMinSimilarity" };

        foreach (var (text1, text2, similarity) in pairs)
        {
            // CSV-escape texts
            var escapedText1 = EscapeCsv(text1);
            var escapedText2 = EscapeCsv(text2);
            lines.Add($"{escapedText1},{escapedText2},{similarity:F2}");
        }

        try
        {
            await File.WriteAllLinesAsync(path, lines);
            GeneratedFiles.Add(path);
        }
        catch (IOException)
        {
            // File may have been created by another test - ignore
        }
    }

    private async Task EnsureBatchTextsFile()
    {
        var path = GetBatchTextsPath();
        if (File.Exists(path))
            return;

        var texts = EmbeddingDataFactory.GenerateBatchTexts(1000);
        try
        {
            await File.WriteAllLinesAsync(path, texts);
            GeneratedFiles.Add(path);
        }
        catch (IOException)
        {
            // File may have been created by another test - ignore
        }
    }

    public async Task EnsureEdgeCasesFile()
    {
        var path = GetEdgeCasesPath();
        if (File.Exists(path))
            return;

        var edgeCases = EmbeddingDataFactory.GenerateEdgeCaseTexts();
        try
        {
            await File.WriteAllLinesAsync(path, edgeCases);
            GeneratedFiles.Add(path);
        }
        catch (IOException)
        {
            // File may have been created by another test - ignore
        }
    }

    private static string EscapeCsv(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "\"\"";

        if (input.Contains('"') || input.Contains(',') || input.Contains('\n'))
        {
            return "\"" + input.Replace("\"", "\"\"") + "\"";
        }

        return input;
    }
}
