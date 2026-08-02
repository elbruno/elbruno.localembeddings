using Bunit;
using ElBruno.LocalEmbeddings.BlazorComponents;
using ElBruno.LocalEmbeddings.BlazorComponents.Components;
using Microsoft.Extensions.AI;
using Moq;
using Xunit;

namespace ElBruno.LocalEmbeddings.BlazorComponents.Tests;

/// <summary>
/// Component render tests for <see cref="SimilarityMeter"/> using bUnit.
/// </summary>
public class SimilarityMeterTests : TestContext
{
    [Fact]
    public void SimilarityMeter_RendersTitle()
    {
        var cut = RenderComponent<SimilarityMeter>();

        Assert.Contains("Similarity Meter", cut.Markup);
    }

    [Fact]
    public void SimilarityMeter_CompareButtonDisabled_WhenNoTexts()
    {
        var cut = RenderComponent<SimilarityMeter>(p => p
            .Add(c => c.Generator, BuildMockGenerator()));

        var button = cut.Find("button");
        Assert.True(button.HasAttribute("disabled"));
    }

    [Fact]
    public void SimilarityMeter_CompareButtonDisabled_WhenNoGenerator()
    {
        var cut = RenderComponent<SimilarityMeter>();

        var button = cut.Find("button");
        Assert.True(button.HasAttribute("disabled"));
    }

    [Fact]
    public void SimilarityMeter_HasTwoTextAreas()
    {
        var cut = RenderComponent<SimilarityMeter>();

        var textareas = cut.FindAll("textarea");
        Assert.Equal(2, textareas.Count);
    }

    [Fact]
    public void SimilarityMeter_RendersWithoutErrors()
    {
        var exception = Record.Exception(() => RenderComponent<SimilarityMeter>());

        Assert.Null(exception);
    }

    private static IEmbeddingGenerator<string, Embedding<float>> BuildMockGenerator()
    {
        var mock = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        mock.Setup(g => g.GenerateAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<EmbeddingGenerationOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> texts, EmbeddingGenerationOptions? _, CancellationToken _) =>
            {
                var embeddings = texts.Select(_ => new Embedding<float>(new float[] { 0.1f, 0.2f })).ToArray();
                return new GeneratedEmbeddings<Embedding<float>>(embeddings);
            });
        return mock.Object;
    }
}
