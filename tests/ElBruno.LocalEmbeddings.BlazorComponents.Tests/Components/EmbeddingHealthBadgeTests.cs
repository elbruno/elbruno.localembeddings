using Bunit;
using ElBruno.LocalEmbeddings.BlazorComponents;
using ElBruno.LocalEmbeddings.BlazorComponents.Components;
using Xunit;

namespace ElBruno.LocalEmbeddings.BlazorComponents.Tests;

/// <summary>
/// Component render tests for <see cref="EmbeddingHealthBadge"/> using bUnit.
/// </summary>
public class EmbeddingHealthBadgeTests : TestContext
{
    [Fact]
    public void EmbeddingHealthBadge_WhenReady_ContainsReadyClass()
    {
        var cut = RenderComponent<EmbeddingHealthBadge>(p => p
            .Add(c => c.IsReady, true)
            .Add(c => c.ModelName, "all-MiniLM-L6-v2"));

        Assert.Contains("ready", cut.Markup);
    }

    [Fact]
    public void EmbeddingHealthBadge_WhenNotReady_ContainsNotReadyClass()
    {
        var cut = RenderComponent<EmbeddingHealthBadge>(p => p
            .Add(c => c.IsReady, false)
            .Add(c => c.ModelName, "all-MiniLM-L6-v2"));

        Assert.Contains("not-ready", cut.Markup);
    }

    [Fact]
    public void EmbeddingHealthBadge_ShowsModelName()
    {
        var cut = RenderComponent<EmbeddingHealthBadge>(p => p
            .Add(c => c.IsReady, true)
            .Add(c => c.ModelName, "bge-small-en-v1.5"));

        Assert.Contains("bge-small-en-v1.5", cut.Markup);
    }

    [Fact]
    public void EmbeddingHealthBadge_WhenHideLabelTrue_DoesNotShowLabel()
    {
        var cut = RenderComponent<EmbeddingHealthBadge>(p => p
            .Add(c => c.IsReady, true)
            .Add(c => c.ModelName, "test-model")
            .Add(c => c.HideLabel, true));

        Assert.DoesNotContain("test-model", cut.Markup);
    }

    [Fact]
    public void EmbeddingHealthBadge_RendersWithoutErrors()
    {
        var exception = Record.Exception(() =>
            RenderComponent<EmbeddingHealthBadge>(p => p
                .Add(c => c.IsReady, true)
                .Add(c => c.ModelName, "test-model")));

        Assert.Null(exception);
    }
}
