using Bunit;
using ElBruno.LocalEmbeddings.BlazorComponents;
using ElBruno.LocalEmbeddings.BlazorComponents.Components;
using Microsoft.Extensions.AI;
using Moq;
using Xunit;

namespace ElBruno.LocalEmbeddings.BlazorComponents.Tests;

/// <summary>
/// Component render tests for <see cref="SemanticSearchBox"/> using bUnit.
/// </summary>
public class SemanticSearchBoxTests : TestContext
{
    private static readonly IReadOnlyList<string> _corpus =
    [
        "Blazor lets you build web UIs using C#.",
        "ONNX Runtime enables machine learning inference.",
        "Semantic search retrieves results by meaning."
    ];

    [Fact]
    public void SemanticSearchBox_HasSearchInput()
    {
        var cut = RenderComponent<SemanticSearchBox>(p => p
            .Add(c => c.Generator, BuildMockGenerator())
            .Add(c => c.Corpus, _corpus));

        // Component uses type="search" (not type="text")
        cut.Find("input[type='search']");
    }

    [Fact]
    public void SemanticSearchBox_UsesCustomPlaceholder()
    {
        var cut = RenderComponent<SemanticSearchBox>(p => p
            .Add(c => c.Generator, BuildMockGenerator())
            .Add(c => c.Corpus, _corpus)
            .Add(c => c.Placeholder, "Search knowledge base…"));

        Assert.Contains("Search knowledge base", cut.Markup);
    }

    [Fact]
    public void SemanticSearchBox_RendersWithoutErrors()
    {
        var exception = Record.Exception(() =>
            RenderComponent<SemanticSearchBox>(p => p
                .Add(c => c.Corpus, _corpus)));

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
                var embeddings = texts.Select(_ => new Embedding<float>(new float[] { 0.5f, 0.5f })).ToArray();
                return new GeneratedEmbeddings<Embedding<float>>(embeddings);
            });
        return mock.Object;
    }
}
