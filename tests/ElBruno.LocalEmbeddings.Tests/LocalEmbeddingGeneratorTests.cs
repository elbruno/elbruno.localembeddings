using ElBruno.LocalEmbeddings.Options;
using Microsoft.Extensions.AI;

namespace ElBruno.LocalEmbeddings.Tests;

public class LocalEmbeddingGeneratorTests
{
    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new LocalEmbeddingGenerator(null!));
    }

    [Fact]
    public void Constructor_WithNoModelPathAndEnsureDownloadedFalse_ThrowsInvalidOperationException()
    {
        var options = new LocalEmbeddingsOptions
        {
            ModelPath = null,
            EnsureModelDownloaded = false
        };

        Assert.Throws<InvalidOperationException>(() => new LocalEmbeddingGenerator(options));
    }

    [Fact]
    public void Constructor_WithNonExistentModelPath_ThrowsFileNotFoundException()
    {
        var options = new LocalEmbeddingsOptions
        {
            ModelPath = "/nonexistent/path/to/model",
            EnsureModelDownloaded = false
        };

        Assert.Throws<FileNotFoundException>(() => new LocalEmbeddingGenerator(options));
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public void Constructor_WithValidModelPath_Succeeds()
    {
        var modelPath = GetModelPath();
        Skip.If(modelPath == null, "Model not available for testing");

        var options = new LocalEmbeddingsOptions
        {
            ModelPath = modelPath,
            EnsureModelDownloaded = false
        };

        using var generator = new LocalEmbeddingGenerator(options);
        Assert.NotNull(generator);
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task GenerateAsync_ReturnCorrectDimensions()
    {
        var modelPath = GetModelPath();
        Skip.If(modelPath == null, "Model not available for testing");

        var options = new LocalEmbeddingsOptions
        {
            ModelPath = modelPath,
            EnsureModelDownloaded = false
        };

        using var generator = new LocalEmbeddingGenerator(options);
        var result = await generator.GenerateAsync(["Hello world"]);

        Assert.Single(result);

        // MiniLM-L6-v2 produces 384-dimensional embeddings
        Assert.Equal(384, result[0].Vector.Length);
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task GenerateAsync_WithEmptyInput_ReturnsEmptyResult()
    {
        var modelPath = GetModelPath();
        Skip.If(modelPath == null, "Model not available for testing");

        var options = new LocalEmbeddingsOptions
        {
            ModelPath = modelPath,
            EnsureModelDownloaded = false
        };

        using var generator = new LocalEmbeddingGenerator(options);
        var result = await generator.GenerateAsync([]);

        Assert.Empty(result);
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task GenerateAsync_WithNullInput_ThrowsArgumentNullException()
    {
        var modelPath = GetModelPath();
        Skip.If(modelPath == null, "Model not available for testing");

        var options = new LocalEmbeddingsOptions
        {
            ModelPath = modelPath,
            EnsureModelDownloaded = false
        };

        using var generator = new LocalEmbeddingGenerator(options);
        await Assert.ThrowsAsync<ArgumentNullException>(() => generator.GenerateAsync(null!));
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task GenerateAsync_BatchProcessing_ProcessesMultipleInputs()
    {
        var modelPath = GetModelPath();
        Skip.If(modelPath == null, "Model not available for testing");

        var options = new LocalEmbeddingsOptions
        {
            ModelPath = modelPath,
            EnsureModelDownloaded = false
        };

        using var generator = new LocalEmbeddingGenerator(options);
        var inputs = new[]
        {
            "The cat sat on the mat",
            "Machine learning is fascinating",
            "Hello, how are you today?"
        };

        var result = await generator.GenerateAsync(inputs);

        Assert.Equal(3, result.Count);
        Assert.All(result, e => Assert.Equal(384, e.Vector.Length));
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task GenerateAsync_SimilarTexts_ProduceSimilarEmbeddings()
    {
        var modelPath = GetModelPath();
        Skip.If(modelPath == null, "Model not available for testing");

        var options = new LocalEmbeddingsOptions
        {
            ModelPath = modelPath,
            EnsureModelDownloaded = false
        };

        using var generator = new LocalEmbeddingGenerator(options);
        var result = await generator.GenerateAsync([
            "The quick brown fox jumps over the lazy dog",
            "A fast brown fox leaps over a sleepy dog"
        ]);

        var similarity = CosineSimilarity(result[0].Vector.ToArray(), result[1].Vector.ToArray());

        // Similar sentences should have high cosine similarity (> 0.7)
        Assert.True(similarity > 0.7, $"Similar texts should have high similarity, got {similarity}");
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task GenerateAsync_DissimilarTexts_ProduceDifferentEmbeddings()
    {
        var modelPath = GetModelPath();
        Skip.If(modelPath == null, "Model not available for testing");

        var options = new LocalEmbeddingsOptions
        {
            ModelPath = modelPath,
            EnsureModelDownloaded = false
        };

        using var generator = new LocalEmbeddingGenerator(options);
        var result = await generator.GenerateAsync([
            "The weather is sunny today",
            "Quantum computing uses qubits"
        ]);

        var similarity = CosineSimilarity(result[0].Vector.ToArray(), result[1].Vector.ToArray());

        // Dissimilar sentences should have lower similarity (< 0.7)
        Assert.True(similarity < 0.7, $"Dissimilar texts should have lower similarity, got {similarity}");
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public void Metadata_ContainsCorrectInformation()
    {
        var modelPath = GetModelPath();
        Skip.If(modelPath == null, "Model not available for testing");

        var options = new LocalEmbeddingsOptions
        {
            ModelPath = modelPath,
            EnsureModelDownloaded = false,
            ModelName = "sentence-transformers/all-MiniLM-L6-v2"
        };

        using var generator = new LocalEmbeddingGenerator(options);

        Assert.NotNull(generator.Metadata);
        Assert.Equal("LocalEmbeddings", generator.Metadata.ProviderName);
        Assert.NotNull(generator.Metadata.ProviderUri);
        Assert.Equal("sentence-transformers/all-MiniLM-L6-v2", generator.Metadata.DefaultModelId);
        Assert.Equal(384, generator.Metadata.DefaultModelDimensions);
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public void GetService_WithSelfType_ReturnsSelf()
    {
        var modelPath = GetModelPath();
        Skip.If(modelPath == null, "Model not available for testing");

        var options = new LocalEmbeddingsOptions
        {
            ModelPath = modelPath,
            EnsureModelDownloaded = false
        };

        using var generator = new LocalEmbeddingGenerator(options);
        var service = generator.GetService<IEmbeddingGenerator<string, Embedding<float>>>();

        Assert.Same(generator, service);
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public void GetService_WithOtherType_ReturnsNull()
    {
        var modelPath = GetModelPath();
        Skip.If(modelPath == null, "Model not available for testing");

        var options = new LocalEmbeddingsOptions
        {
            ModelPath = modelPath,
            EnsureModelDownloaded = false
        };

        using var generator = new LocalEmbeddingGenerator(options);
        var service = generator.GetService<IDisposable>();

        Assert.Null(service);
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public void GetService_NonGenericWithSelfType_ReturnsSelf()
    {
        var modelPath = GetModelPath();
        Skip.If(modelPath == null, "Model not available for testing");

        var options = new LocalEmbeddingsOptions
        {
            ModelPath = modelPath,
            EnsureModelDownloaded = false
        };

        using var generator = new LocalEmbeddingGenerator(options);
        var service = generator.GetService(typeof(IEmbeddingGenerator<string, Embedding<float>>));

        Assert.Same(generator, service);
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public void GetService_NonGenericWithNullType_ThrowsArgumentNullException()
    {
        var modelPath = GetModelPath();
        Skip.If(modelPath == null, "Model not available for testing");

        var options = new LocalEmbeddingsOptions
        {
            ModelPath = modelPath,
            EnsureModelDownloaded = false
        };

        using var generator = new LocalEmbeddingGenerator(options);
        Assert.Throws<ArgumentNullException>(() => generator.GetService(null!));
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task GenerateAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var modelPath = GetModelPath();
        Skip.If(modelPath == null, "Model not available for testing");

        var options = new LocalEmbeddingsOptions
        {
            ModelPath = modelPath,
            EnsureModelDownloaded = false
        };

        var generator = new LocalEmbeddingGenerator(options);
        generator.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => generator.GenerateAsync(["test"]));
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public void Dispose_MultipleTimes_DoesNotThrow()
    {
        var modelPath = GetModelPath();
        Skip.If(modelPath == null, "Model not available for testing");

        var options = new LocalEmbeddingsOptions
        {
            ModelPath = modelPath,
            EnsureModelDownloaded = false
        };

        var generator = new LocalEmbeddingGenerator(options);

        // Should not throw on multiple dispose calls
        generator.Dispose();
        generator.Dispose();
        generator.Dispose();
    }

    /// <summary>
    /// Gets the path to a model directory if available (for integration tests).
    /// </summary>
    private static string? GetModelPath()
    {
        // Check default cache location
        var defaultCache = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LocalEmbeddings", "models")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "LocalEmbeddings", "models");

        var modelDir = Path.Combine(defaultCache, "sentence-transformers_all-MiniLM-L6-v2");

        if (Directory.Exists(modelDir) && File.Exists(Path.Combine(modelDir, "model.onnx")))
            return modelDir;

        // Try alternative locations
        var envPath = Environment.GetEnvironmentVariable("LOCALEMBEDDINGS_TEST_MODEL");
        if (!string.IsNullOrEmpty(envPath) && Directory.Exists(envPath))
            return envPath;

        return null;
    }

    /// <summary>
    /// Calculates cosine similarity between two vectors.
    /// </summary>
    private static double CosineSimilarity(float[] a, float[] b)
    {
        double dotProduct = 0;
        double normA = 0;
        double normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
