using Bunit;
using ElBruno.LocalEmbeddings.BlazorComponents;
using ElBruno.LocalEmbeddings.BlazorComponents.Components;
using Xunit;

namespace ElBruno.LocalEmbeddings.BlazorComponents.Tests;

/// <summary>
/// Component render tests for <see cref="EmbeddingModelSelector"/> using bUnit.
/// </summary>
public class EmbeddingModelSelectorTests : TestContext
{
    [Fact]
    public void EmbeddingModelSelector_ShowsModelDisplayNames()
    {
        var models = new[]
        {
            MakeModel("m/alpha", "Alpha"),
            MakeModel("m/beta", "Beta"),
        };

        var cut = RenderComponent<EmbeddingModelSelector>(p => p
            .Add(c => c.Models, models));

        Assert.Contains("Alpha", cut.Markup);
        Assert.Contains("Beta", cut.Markup);
    }

    [Fact]
    public void EmbeddingModelSelector_HasSelectElement()
    {
        var models = new[] { MakeModel("m/alpha", "Alpha") };

        var cut = RenderComponent<EmbeddingModelSelector>(p => p
            .Add(c => c.Models, models));

        cut.Find("select");
    }

    [Fact]
    public void EmbeddingModelSelector_WhenDisabled_SelectIsDisabled()
    {
        var models = new[] { MakeModel("m/alpha", "Alpha") };

        var cut = RenderComponent<EmbeddingModelSelector>(p => p
            .Add(c => c.Models, models)
            .Add(c => c.Disabled, true));

        var select = cut.Find("select");
        Assert.True(select.HasAttribute("disabled"));
    }

    [Fact]
    public void EmbeddingModelSelector_RendersWithoutErrors()
    {
        var exception = Record.Exception(() =>
            RenderComponent<EmbeddingModelSelector>(p => p
                .Add(c => c.Models, Array.Empty<EmbeddingModelInfo>())));

        Assert.Null(exception);
    }

    private static EmbeddingModelInfo MakeModel(string id, string name) =>
        new()
        {
            ModelId = id,
            DisplayName = name,
            Language = "English",
            Dimensions = 384,
            SizeMb = 23
        };
}
