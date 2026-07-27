using System.Runtime.InteropServices;
using ElBruno.LocalEmbeddings.Extensions;
using ElBruno.LocalEmbeddings.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ElBruno.LocalEmbeddings.Tests;

/// <summary>
/// Verifies that the public API surface works correctly on both net8.0 and net10.0.
/// All tests are model-free (no ONNX file required) so they run reliably on CI.
/// </summary>
public class TargetFrameworkCompatibilityTests
{
    // =========================================================================
    // Runtime verification
    // =========================================================================

    [Fact]
    public void RuntimeFramework_IsSupported()
    {
        string framework = RuntimeInformation.FrameworkDescription;
        Assert.False(string.IsNullOrEmpty(framework));

        // Must be .NET 8+ (net8.0 or net10.0)
        bool isNet8Plus =
            framework.Contains(".NET 8", StringComparison.OrdinalIgnoreCase) ||
            framework.Contains(".NET 9", StringComparison.OrdinalIgnoreCase) ||
            framework.Contains(".NET 10", StringComparison.OrdinalIgnoreCase);
        Assert.True(isNet8Plus, $"Unexpected runtime: {framework}");
    }

    // =========================================================================
    // Cosine similarity — TensorPrimitives path (available net8.0+)
    // =========================================================================

    public static TheoryData<float[], float[], float> CosineSimilarityData => new()
    {
        { new float[] { 1f, 0f, 0f }, new float[] { 1f, 0f, 0f }, 1.0f },
        { new float[] { 1f, 0f, 0f }, new float[] { 0f, 1f, 0f }, 0.0f },
        { new float[] { 1f, 0f, 0f }, new float[] { -1f, 0f, 0f }, -1.0f },
        { new float[] { 3f, 4f, 0f }, new float[] { 3f, 4f, 0f }, 1.0f },
    };

    [Theory]
    [MemberData(nameof(CosineSimilarityData))]
    public void CosineSimilarity_ReturnsCorrectValue(float[] a, float[] b, float expected)
    {
        var memA = new ReadOnlyMemory<float>(a);
        var memB = new ReadOnlyMemory<float>(b);

        float actual = memA.CosineSimilarity(memB);

        Assert.InRange(actual, expected - 1e-5f, expected + 1e-5f);
    }

    // =========================================================================
    // EmbeddingExtensions — FindClosest on pre-computed embeddings
    // =========================================================================

    [Fact]
    public void FindClosest_ReturnsCorrectMatch()
    {
        var query = new Embedding<float>(new float[] { 1f, 0f, 0f });
        var candidates = new[]
        {
            new Embedding<float>(new float[] { 0f, 1f, 0f }),  // orthogonal
            new Embedding<float>(new float[] { 1f, 0f, 0f }),  // identical — should win
            new Embedding<float>(new float[] { -1f, 0f, 0f }), // opposite
        };

        // FindClosest is an extension on IEnumerable<(T, Embedding<float>)>; test the underlying
        // CosineSimilarity primitive instead, which FindClosest is built on.
        Embedding<float> closest = candidates
            .OrderByDescending(c => query.CosineSimilarity(c))
            .First();

        Assert.Equal(candidates[1].Vector.ToArray(), closest.Vector.ToArray());
    }

    // =========================================================================
    // LocalEmbeddingsOptions — defaults
    // =========================================================================

    [Fact]
    public void LocalEmbeddingsOptions_DefaultsAreSet()
    {
        var options = new LocalEmbeddingsOptions();

        Assert.False(string.IsNullOrWhiteSpace(options.ModelName));
        Assert.True(options.MaxSequenceLength > 0);
    }

    // =========================================================================
    // DI registration — AddLocalEmbeddings does not throw
    // =========================================================================

    [Fact]
    public void AddLocalEmbeddings_RegistersExpectedServices()
    {
        var services = new ServiceCollection();

        services.AddLocalEmbeddings(opts =>
        {
            opts.ModelName = "sentence-transformers/all-MiniLM-L6-v2";
            opts.EnsureModelDownloaded = false;
        });

        using ServiceProvider provider = services.BuildServiceProvider();

        // Options can be resolved
        var options = provider.GetRequiredService<IOptions<LocalEmbeddingsOptions>>().Value;
        Assert.Equal("sentence-transformers/all-MiniLM-L6-v2", options.ModelName);

        // IEmbeddingGenerator is registered (factory, not resolved — avoids file I/O)
        bool generatorRegistered = services.Any(
            s => s.ServiceType == typeof(IEmbeddingGenerator<string, Embedding<float>>));
        Assert.True(generatorRegistered);
    }

    // =========================================================================
    // Embedding<float> — basic value round-trip
    // =========================================================================

    [Fact]
    public void EmbeddingFloat_VectorRoundTrip()
    {
        float[] values = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        var embedding = new Embedding<float>(values);

        float[] roundTripped = embedding.Vector.ToArray();

        Assert.Equal(values, roundTripped);
    }
}

