using ElBruno.LocalEmbeddings.BlazorComponents;
using Microsoft.Extensions.AI;
using Moq;
using Xunit;

namespace ElBruno.LocalEmbeddings.BlazorComponents.Tests;

/// <summary>
/// Tests for <see cref="EmbeddingStateService"/>.
/// </summary>
public class EmbeddingStateServiceTests
{
    // ── CosineSimilarity ──────────────────────────────────────────────────

    [Fact]
    public void CosineSimilarity_IdenticalVectors_ReturnsOne()
    {
        float[] v = [1f, 2f, 3f];
        float result = EmbeddingStateService.CosineSimilarity(v, v);
        Assert.Equal(1f, result, precision: 5);
    }

    [Fact]
    public void CosineSimilarity_OppositeVectors_ReturnsNegativeOne()
    {
        float[] a = [1f, 0f, 0f];
        float[] b = [-1f, 0f, 0f];
        float result = EmbeddingStateService.CosineSimilarity(a, b);
        Assert.Equal(-1f, result, precision: 5);
    }

    [Fact]
    public void CosineSimilarity_OrthogonalVectors_ReturnsZero()
    {
        float[] a = [1f, 0f, 0f];
        float[] b = [0f, 1f, 0f];
        float result = EmbeddingStateService.CosineSimilarity(a, b);
        Assert.Equal(0f, result, precision: 5);
    }

    [Fact]
    public void CosineSimilarity_ZeroVectors_ReturnsZero()
    {
        float[] z = [0f, 0f, 0f];
        float result = EmbeddingStateService.CosineSimilarity(z, z);
        Assert.Equal(0f, result);
    }

    [Fact]
    public void CosineSimilarity_NullFirstArgument_ThrowsArgumentNullException()
    {
        float[] b = [1f, 2f];
        Assert.Throws<ArgumentNullException>(() => EmbeddingStateService.CosineSimilarity(null!, b));
    }

    [Fact]
    public void CosineSimilarity_NullSecondArgument_ThrowsArgumentNullException()
    {
        float[] a = [1f, 2f];
        Assert.Throws<ArgumentNullException>(() => EmbeddingStateService.CosineSimilarity(a, null!));
    }

    [Fact]
    public void CosineSimilarity_DifferentLengths_ThrowsArgumentException()
    {
        float[] a = [1f, 2f];
        float[] b = [1f, 2f, 3f];
        Assert.Throws<ArgumentException>(() => EmbeddingStateService.CosineSimilarity(a, b));
    }

    [Theory]
    [InlineData(new float[] { 0.5f, 0.5f }, new float[] { 0.5f, 0.5f }, 1f)]
    [InlineData(new float[] { 1f, 0f }, new float[] { 0f, 1f }, 0f)]
    [InlineData(new float[] { 3f, 4f }, new float[] { 3f, 4f }, 1f)]
    public void CosineSimilarity_TableDriven(float[] a, float[] b, float expected)
    {
        float result = EmbeddingStateService.CosineSimilarity(a, b);
        Assert.Equal(expected, result, precision: 4);
    }

    // ── Models / SelectedModelId / SelectedModel ──────────────────────────

    [Fact]
    public void Models_ReturnsNonEmptyList()
    {
        var svc = new EmbeddingStateService();
        Assert.NotEmpty(svc.Models);
    }

    [Fact]
    public void Models_ContainsMiniLmModel()
    {
        var svc = new EmbeddingStateService();
        Assert.Contains(svc.Models, m => m.ModelId.Contains("MiniLM"));
    }

    [Fact]
    public void SelectedModelId_InitiallyNull()
    {
        var svc = new EmbeddingStateService();
        Assert.Null(svc.SelectedModelId);
    }

    [Fact]
    public void SelectedModel_WhenNoIdSet_ReturnsNull()
    {
        var svc = new EmbeddingStateService();
        Assert.Null(svc.SelectedModel);
    }

    [Fact]
    public void SelectedModel_WhenIdSet_ReturnsMatchingModel()
    {
        var svc = new EmbeddingStateService();
        string firstId = svc.Models[0].ModelId;

        svc.SelectedModelId = firstId;

        Assert.NotNull(svc.SelectedModel);
        Assert.Equal(firstId, svc.SelectedModel!.ModelId);
    }

    [Fact]
    public void SelectedModelId_WhenSet_RaisesSelectedModelChangedEvent()
    {
        var svc = new EmbeddingStateService();
        bool eventRaised = false;
        svc.SelectedModelChanged += (_, _) => eventRaised = true;

        svc.SelectedModelId = svc.Models[0].ModelId;

        Assert.True(eventRaised);
    }

    [Fact]
    public void SelectedModelId_SameValueAgain_DoesNotRaiseEvent()
    {
        var svc = new EmbeddingStateService();
        string id = svc.Models[0].ModelId;
        svc.SelectedModelId = id;

        int eventCount = 0;
        svc.SelectedModelChanged += (_, _) => eventCount++;

        svc.SelectedModelId = id; // set the same value again

        Assert.Equal(0, eventCount);
    }

    [Fact]
    public void SelectedModel_UnknownId_ReturnsNull()
    {
        var svc = new EmbeddingStateService();
        svc.SelectedModelId = "nonexistent/model-id";

        Assert.Null(svc.SelectedModel);
    }

    // ── GenerateEmbeddingsAsync ───────────────────────────────────────────

    [Fact]
    public async Task GenerateEmbeddingsAsync_NullGenerator_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => EmbeddingStateService.GenerateEmbeddingsAsync(null!, ["text"]));
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_NullTexts_ThrowsArgumentNullException()
    {
        var mockGenerator = BuildMockGenerator([0.1f, 0.2f, 0.3f]);
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => EmbeddingStateService.GenerateEmbeddingsAsync(mockGenerator, null!));
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_TwoTexts_ReturnsTwoEmbeddings()
    {
        var mockGenerator = BuildMockGenerator([0.1f, 0.2f, 0.3f]);

        float[][] result = await EmbeddingStateService.GenerateEmbeddingsAsync(
            mockGenerator, ["hello", "world"]);

        Assert.Equal(2, result.Length);
        Assert.Equal(3, result[0].Length);
        Assert.Equal(3, result[1].Length);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_SingleText_ReturnsOneEmbedding()
    {
        var mockGenerator = BuildMockGenerator([1f, 0f]);

        float[][] result = await EmbeddingStateService.GenerateEmbeddingsAsync(
            mockGenerator, ["hello"]);

        Assert.Single(result);
        Assert.Equal(new float[] { 1f, 0f }, result[0]);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static IEmbeddingGenerator<string, Embedding<float>> BuildMockGenerator(float[] vector)
    {
        var mock = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        mock.Setup(g => g.GenerateAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<EmbeddingGenerationOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> texts, EmbeddingGenerationOptions? _, CancellationToken _) =>
            {
                var embeddings = texts.Select(_ => new Embedding<float>(vector)).ToArray();
                return new GeneratedEmbeddings<Embedding<float>>(embeddings);
            });
        return mock.Object;
    }
}
