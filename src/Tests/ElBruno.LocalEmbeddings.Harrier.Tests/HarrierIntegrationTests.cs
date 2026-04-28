using ElBruno.LocalEmbeddings.Harrier.Options;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalEmbeddings.Harrier.Tests;

/// <summary>
/// Integration tests for Harrier embedding model that require real model files.
/// All tests use SkippableFact and skip gracefully when model files are unavailable.
/// </summary>
[Trait("Category", "Integration")]
public class HarrierIntegrationTests
{
    [SkippableFact]
    public async Task CreateAsync_WithRealModel_ReturnsGenerator()
    {
        var modelDir = FindHarrierModelDirectory();
        Skip.If(modelDir is null, "Harrier model not available locally — skipping.");

        using var generator = await HarrierEmbeddingGenerator.CreateAsync(new HarrierEmbeddingsOptions
        {
            ModelPath = modelDir,
            EnsureModelDownloaded = false
        });

        Assert.NotNull(generator);
        Assert.NotNull(generator.Metadata);
        Assert.Equal(HarrierEmbeddingsOptions.DefaultModelName, generator.Metadata.DefaultModelId);
    }

    [SkippableFact]
    public async Task GenerateAsync_ProducesValidEmbeddings()
    {
        var modelDir = FindHarrierModelDirectory();
        Skip.If(modelDir is null, "Harrier model not available locally — skipping.");

        using var generator = await HarrierEmbeddingGenerator.CreateAsync(new HarrierEmbeddingsOptions
        {
            ModelPath = modelDir,
            EnsureModelDownloaded = false
        });

        var result = await generator.GenerateAsync(["The weather is beautiful today"]);

        Assert.Single(result);
        Assert.Equal(640, result[0].Vector.Length);

        // Verify vector contains non-zero values
        Assert.True(result[0].Vector.ToArray().Any(v => v != 0f), "Embedding vector should have non-zero values");
    }

    [SkippableFact]
    public async Task GenerateAsync_DeterministicOutput()
    {
        var modelDir = FindHarrierModelDirectory();
        Skip.If(modelDir is null, "Harrier model not available locally — skipping.");

        using var generator = await HarrierEmbeddingGenerator.CreateAsync(new HarrierEmbeddingsOptions
        {
            ModelPath = modelDir,
            EnsureModelDownloaded = false
        });

        const string text = "Deterministic output verification";
        var result1 = await generator.GenerateAsync([text]);
        var result2 = await generator.GenerateAsync([text]);

        var v1 = result1[0].Vector.ToArray();
        var v2 = result2[0].Vector.ToArray();

        Assert.Equal(v1.Length, v2.Length);
        for (int i = 0; i < v1.Length; i++)
        {
            Assert.Equal(v1[i], v2[i], 5);
        }
    }

    [SkippableFact]
    public void Tokenize_KnownInput_ProducesValidTokenIds()
    {
        var modelDir = FindHarrierModelDirectory();
        Skip.If(modelDir is null, "Harrier model not available locally — skipping.");

        var tokenizer = HarrierTokenizer.Create(modelDir, maxLength: 128);
        var (inputIds, attentionMask) = tokenizer.Tokenize("Hello world");

        Assert.Equal(128, inputIds.Length);
        Assert.Equal(128, attentionMask.Length);

        // BOS token should be at position 0
        Assert.Equal(2L, inputIds[0]);
        Assert.Equal(1L, attentionMask[0]);

        // Should have at least BOS + some content tokens + EOS
        int activeTokens = attentionMask.Count(m => m == 1);
        Assert.True(activeTokens >= 3, $"Expected at least 3 active tokens (BOS + content + EOS), got {activeTokens}");

        // Verify EOS token is present (id=1)
        bool hasEos = false;
        for (int i = 1; i < inputIds.Length; i++)
        {
            if (inputIds[i] == 1L && attentionMask[i] == 1L)
            {
                hasEos = true;
                break;
            }
        }

        Assert.True(hasEos, "Expected EOS token (id=1) in the active region");
    }

    private static string? FindHarrierModelDirectory()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var paths = new[]
        {
            Path.Combine(appData, "ElBruno", "LocalEmbeddings", "models", "onnx-community_harrier-oss-v1-270m-ONNX"),
            Path.Combine(appData, "LocalEmbeddings", "models", "onnx-community_harrier-oss-v1-270m-ONNX"),
        };

        return paths.FirstOrDefault(p =>
            Directory.Exists(p) &&
            File.Exists(Path.Combine(p, "tokenizer.json")) &&
            (File.Exists(Path.Combine(p, "model.onnx")) ||
             File.Exists(Path.Combine(p, "model_quantized.onnx")) ||
             File.Exists(Path.Combine(p, "model_q4.onnx")) ||
             File.Exists(Path.Combine(p, "model_fp16.onnx"))));
    }
}
