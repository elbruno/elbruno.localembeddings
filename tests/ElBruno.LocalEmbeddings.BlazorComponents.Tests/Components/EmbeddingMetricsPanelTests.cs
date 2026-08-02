using Bunit;
using ElBruno.LocalEmbeddings.BlazorComponents;
using ElBruno.LocalEmbeddings.BlazorComponents.Components;
using Xunit;

namespace ElBruno.LocalEmbeddings.BlazorComponents.Tests;

/// <summary>
/// Component render tests for <see cref="EmbeddingMetricsPanel"/> using bUnit.
/// </summary>
public class EmbeddingMetricsPanelTests : TestContext
{
    [Fact]
    public void EmbeddingMetricsPanel_DisplaysTokensPerSecond()
    {
        var cut = RenderComponent<EmbeddingMetricsPanel>(p => p
            .Add(c => c.TokensPerSecond, 1500.0)
            .Add(c => c.EmbeddingDimension, 384)
            .Add(c => c.BatchSize, 32)
            .Add(c => c.MemoryUsageMb, 64.5));

        // Component formats with "N0" + InvariantCulture → "1,500"
        Assert.Contains("1,500", cut.Markup);
    }

    [Fact]
    public void EmbeddingMetricsPanel_DisplaysEmbeddingDimension()
    {
        var cut = RenderComponent<EmbeddingMetricsPanel>(p => p
            .Add(c => c.TokensPerSecond, 1000)
            .Add(c => c.EmbeddingDimension, 768)
            .Add(c => c.BatchSize, 16)
            .Add(c => c.MemoryUsageMb, 100.0));

        Assert.Contains("768", cut.Markup);
    }

    [Fact]
    public void EmbeddingMetricsPanel_DisplaysBatchSize()
    {
        var cut = RenderComponent<EmbeddingMetricsPanel>(p => p
            .Add(c => c.TokensPerSecond, 1000)
            .Add(c => c.EmbeddingDimension, 384)
            .Add(c => c.BatchSize, 64)
            .Add(c => c.MemoryUsageMb, 50.0));

        Assert.Contains("64", cut.Markup);
    }

    [Fact]
    public void EmbeddingMetricsPanel_RendersWithoutErrors()
    {
        var exception = Record.Exception(() =>
            RenderComponent<EmbeddingMetricsPanel>(p => p
                .Add(c => c.TokensPerSecond, 0)
                .Add(c => c.EmbeddingDimension, 384)
                .Add(c => c.BatchSize, 1)
                .Add(c => c.MemoryUsageMb, 0)));

        Assert.Null(exception);
    }
}
