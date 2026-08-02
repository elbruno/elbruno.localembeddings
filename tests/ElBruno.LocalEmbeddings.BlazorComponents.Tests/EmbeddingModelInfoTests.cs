using ElBruno.LocalEmbeddings.BlazorComponents;
using Xunit;

namespace ElBruno.LocalEmbeddings.BlazorComponents.Tests;

/// <summary>
/// Tests for <see cref="EmbeddingModelInfo"/> and <see cref="EmbeddingModelState"/>.
/// </summary>
public class EmbeddingModelInfoTests
{
    [Fact]
    public void EmbeddingModelInfo_DefaultState_IsNotDownloaded()
    {
        var model = new EmbeddingModelInfo
        {
            ModelId = "test/model",
            DisplayName = "Test Model"
        };

        Assert.Equal(EmbeddingModelState.NotDownloaded, model.State);
    }

    [Fact]
    public void EmbeddingModelInfo_DefaultDownloadProgress_IsZero()
    {
        var model = new EmbeddingModelInfo
        {
            ModelId = "test/model",
            DisplayName = "Test Model"
        };

        Assert.Equal(0, model.DownloadProgressPercent);
    }

    [Fact]
    public void EmbeddingModelInfo_DefaultLanguage_IsEnglish()
    {
        var model = new EmbeddingModelInfo
        {
            ModelId = "test/model",
            DisplayName = "Test Model"
        };

        Assert.Equal("English", model.Language);
    }

    [Fact]
    public void EmbeddingModelInfo_CanSetStateToDownloading()
    {
        var model = new EmbeddingModelInfo
        {
            ModelId = "test/model",
            DisplayName = "Test Model",
            State = EmbeddingModelState.Downloading
        };

        Assert.Equal(EmbeddingModelState.Downloading, model.State);
    }

    [Fact]
    public void EmbeddingModelInfo_CanUpdateDownloadProgress()
    {
        var model = new EmbeddingModelInfo
        {
            ModelId = "test/model",
            DisplayName = "Test Model"
        };

        model.DownloadProgressPercent = 75;

        Assert.Equal(75, model.DownloadProgressPercent);
    }

    [Fact]
    public void EmbeddingModelInfo_CanSetStateToDownloaded()
    {
        var model = new EmbeddingModelInfo
        {
            ModelId = "test/model",
            DisplayName = "Test Model"
        };

        model.State = EmbeddingModelState.Downloaded;

        Assert.Equal(EmbeddingModelState.Downloaded, model.State);
    }

    [Theory]
    [InlineData("sentence-transformers/all-MiniLM-L6-v2", "all-MiniLM-L6-v2", 384, 23)]
    [InlineData("BAAI/bge-small-en-v1.5", "bge-small-en-v1.5", 384, 33)]
    [InlineData("BAAI/bge-m3", "bge-m3", 1024, 570)]
    public void EmbeddingModelInfo_InitialisesAllProperties(
        string modelId, string displayName, int dimensions, double sizeMb)
    {
        var model = new EmbeddingModelInfo
        {
            ModelId = modelId,
            DisplayName = displayName,
            Dimensions = dimensions,
            SizeMb = sizeMb,
            Language = "English",
            Description = "Test description."
        };

        Assert.Equal(modelId, model.ModelId);
        Assert.Equal(displayName, model.DisplayName);
        Assert.Equal(dimensions, model.Dimensions);
        Assert.Equal(sizeMb, model.SizeMb);
    }
}
