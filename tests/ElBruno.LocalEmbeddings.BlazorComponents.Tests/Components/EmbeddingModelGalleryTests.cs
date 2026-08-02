using Bunit;
using ElBruno.LocalEmbeddings.BlazorComponents;
using ElBruno.LocalEmbeddings.BlazorComponents.Components;
using Xunit;

namespace ElBruno.LocalEmbeddings.BlazorComponents.Tests;

/// <summary>
/// Component render tests for <see cref="EmbeddingModelGallery"/> using bUnit.
/// </summary>
public class EmbeddingModelGalleryTests : TestContext
{
    [Fact]
    public void EmbeddingModelGallery_ShowsAllModelNames()
    {
        var models = new[]
        {
            MakeModel("m/alpha", "Alpha Model"),
            MakeModel("m/beta",  "Beta Model"),
        };

        var cut = RenderComponent<EmbeddingModelGallery>(p => p
            .Add(c => c.Models, models));

        Assert.Contains("Alpha Model", cut.Markup);
        Assert.Contains("Beta Model", cut.Markup);
    }

    [Fact]
    public void EmbeddingModelGallery_EmptyModels_RendersWithoutErrors()
    {
        var exception = Record.Exception(() =>
            RenderComponent<EmbeddingModelGallery>(p => p
                .Add(c => c.Models, Array.Empty<EmbeddingModelInfo>())));

        Assert.Null(exception);
    }

    [Fact]
    public void EmbeddingModelGallery_RendersWithoutErrors()
    {
        var models = new[] { MakeModel("test/model", "Test Model") };

        var exception = Record.Exception(() =>
            RenderComponent<EmbeddingModelGallery>(p => p
                .Add(c => c.Models, models)));

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
