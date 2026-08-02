using Bunit;
using ElBruno.LocalEmbeddings.BlazorComponents;
using ElBruno.LocalEmbeddings.BlazorComponents.Components;
using Xunit;

namespace ElBruno.LocalEmbeddings.BlazorComponents.Tests;

/// <summary>
/// Component render tests for <see cref="EmbeddingDimensionViewer"/> using bUnit.
/// </summary>
public class EmbeddingDimensionViewerTests : TestContext
{
    [Fact]
    public void EmbeddingDimensionViewer_WhenNoData_ShowsEmptyMessage()
    {
        var cut = RenderComponent<EmbeddingDimensionViewer>();

        Assert.Contains("Provide Labels", cut.Markup);
    }

    [Fact]
    public void EmbeddingDimensionViewer_WithData_RendersSvg()
    {
        var labels = new[] { "Sentence A", "Sentence B", "Sentence C" };
        var embeddings = new[]
        {
            new float[] { 1f, 0f, 0f },
            new float[] { 0f, 1f, 0f },
            new float[] { 0f, 0f, 1f },
        };

        var cut = RenderComponent<EmbeddingDimensionViewer>(p => p
            .Add(c => c.Labels, labels)
            .Add(c => c.Embeddings, embeddings));

        Assert.Contains("<svg", cut.Markup);
    }

    [Fact]
    public void EmbeddingDimensionViewer_WithData_ShowsLabelText()
    {
        var labels = new[] { "Alpha", "Beta" };
        var embeddings = new[]
        {
            new float[] { 1f, 0f },
            new float[] { 0f, 1f },
        };

        var cut = RenderComponent<EmbeddingDimensionViewer>(p => p
            .Add(c => c.Labels, labels)
            .Add(c => c.Embeddings, embeddings));

        // SVG text labels are rendered via MarkupString — should appear in the rendered HTML
        Assert.Contains("Alpha", cut.Markup);
        Assert.Contains("Beta", cut.Markup);
    }

    [Fact]
    public void EmbeddingDimensionViewer_ShowsCustomTitle()
    {
        var cut = RenderComponent<EmbeddingDimensionViewer>(p => p
            .Add(c => c.Title, "My Custom Plot"));

        Assert.Contains("My Custom Plot", cut.Markup);
    }

    [Fact]
    public void EmbeddingDimensionViewer_RendersWithoutErrors()
    {
        var exception = Record.Exception(() =>
            RenderComponent<EmbeddingDimensionViewer>());

        Assert.Null(exception);
    }
}
