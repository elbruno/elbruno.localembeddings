using Bunit;
using ElBruno.LocalEmbeddings.BlazorComponents;
using ElBruno.LocalEmbeddings.BlazorComponents.Components;
using Xunit;

namespace ElBruno.LocalEmbeddings.BlazorComponents.Tests;

/// <summary>
/// Component render tests for <see cref="EmbeddingModelStatusCard"/> using bUnit.
/// </summary>
public class EmbeddingModelStatusCardTests : TestContext
{
    [Fact]
    public void EmbeddingModelStatusCard_ShowsDisplayName()
    {
        var model = MakeModel("test/model", "My Test Model");

        var cut = RenderComponent<EmbeddingModelStatusCard>(p => p
            .Add(c => c.Model, model));

        Assert.Contains("My Test Model", cut.Markup);
    }

    [Fact]
    public void EmbeddingModelStatusCard_WhenNotDownloaded_ShowsDownloadButton()
    {
        var model = MakeModel("test/model", "Test", EmbeddingModelState.NotDownloaded);

        var cut = RenderComponent<EmbeddingModelStatusCard>(p => p
            .Add(c => c.Model, model));

        Assert.Contains("Download", cut.Markup);
    }

    [Fact]
    public void EmbeddingModelStatusCard_WhenDownloaded_ShowsDeleteButton()
    {
        var model = MakeModel("test/model", "Test", EmbeddingModelState.Downloaded);

        var cut = RenderComponent<EmbeddingModelStatusCard>(p => p
            .Add(c => c.Model, model));

        Assert.Contains("Delete", cut.Markup);
    }

    [Fact]
    public void EmbeddingModelStatusCard_WhenDownloading_ShowsCancelButton()
    {
        var model = MakeModel("test/model", "Test", EmbeddingModelState.Downloading);
        model.DownloadProgressPercent = 50;

        var cut = RenderComponent<EmbeddingModelStatusCard>(p => p
            .Add(c => c.Model, model));

        Assert.Contains("Cancel", cut.Markup);
    }

    [Fact]
    public void EmbeddingModelStatusCard_ShowsModelDescription()
    {
        var model = new EmbeddingModelInfo
        {
            ModelId = "test/model",
            DisplayName = "Test",
            Description = "A fast, compact model."
        };

        var cut = RenderComponent<EmbeddingModelStatusCard>(p => p
            .Add(c => c.Model, model));

        Assert.Contains("A fast, compact model.", cut.Markup);
    }

    [Fact]
    public void EmbeddingModelStatusCard_RendersWithoutErrors()
    {
        var model = MakeModel("test/model", "Test");

        var exception = Record.Exception(() =>
            RenderComponent<EmbeddingModelStatusCard>(p => p
                .Add(c => c.Model, model)));

        Assert.Null(exception);
    }

    private static EmbeddingModelInfo MakeModel(
        string id, string name,
        EmbeddingModelState state = EmbeddingModelState.NotDownloaded) =>
        new()
        {
            ModelId = id,
            DisplayName = name,
            State = state,
            Dimensions = 384,
            SizeMb = 23,
            Language = "English"
        };
}
