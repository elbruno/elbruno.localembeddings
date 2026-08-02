using Bunit;
using ElBruno.LocalEmbeddings.BlazorComponents;
using ElBruno.LocalEmbeddings.BlazorComponents.Components;
using Microsoft.Extensions.AI;
using Moq;
using Xunit;

namespace ElBruno.LocalEmbeddings.BlazorComponents.Tests;

/// <summary>
/// Component render tests for <see cref="EmbeddingExplorer"/> using bUnit.
/// </summary>
public class EmbeddingExplorerTests : TestContext
{
    [Fact]
    public void EmbeddingExplorer_RendersTitle()
    {
        var cut = RenderComponent<EmbeddingExplorer>();

        Assert.Contains("Embedding Explorer", cut.Markup);
    }

    [Fact]
    public void EmbeddingExplorer_HasTwoSentenceInputsInitially()
    {
        var cut = RenderComponent<EmbeddingExplorer>();

        var inputs = cut.FindAll("input.sentence-input");
        Assert.Equal(2, inputs.Count);
    }

    [Fact]
    public void EmbeddingExplorer_ComputeButtonDisabled_WhenNoGenerator()
    {
        var cut = RenderComponent<EmbeddingExplorer>();

        var button = cut.Find("button.btn-primary");
        Assert.True(button.HasAttribute("disabled"));
    }

    [Fact]
    public void EmbeddingExplorer_AddSentenceButtonVisible_WhenFewSentences()
    {
        var cut = RenderComponent<EmbeddingExplorer>();

        Assert.Contains("Add sentence", cut.Markup);
    }

    [Fact]
    public void EmbeddingExplorer_RendersWithoutErrors()
    {
        var exception = Record.Exception(() => RenderComponent<EmbeddingExplorer>());

        Assert.Null(exception);
    }
}
