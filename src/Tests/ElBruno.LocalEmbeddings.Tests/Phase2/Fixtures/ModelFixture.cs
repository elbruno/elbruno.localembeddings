using System;
using System.Threading.Tasks;
using ElBruno.LocalEmbeddings.Options;
using ElBruno.LocalEmbeddings.Tests.Phase2.Helpers;

namespace ElBruno.LocalEmbeddings.Tests.Phase2.Fixtures;

/// <summary>
/// Shared fixture for model management across Phase 2 tests.
/// Handles model caching, loading, and cleanup.
/// Uses a cached model directory to avoid repeated downloads.
/// </summary>
public class ModelFixture : IAsyncLifetime
{
    private readonly string _modelCacheDirectory;
    private LocalEmbeddingsOptions? _options;

    public ModelFixture()
    {
        _modelCacheDirectory = Path.Combine(Path.GetTempPath(), "elbruno-model-cache");
    }

    public LocalEmbeddingsOptions Options => _options ?? throw new InvalidOperationException("Fixture not initialized");

    public async Task InitializeAsync()
    {
        // Ensure model cache directory exists
        Directory.CreateDirectory(_modelCacheDirectory);

        // Create default options pointing to cache directory
        _options = EmbeddingDataFactory.GenerateOptionsWithConfiguration(
            modelName: "sentence-transformers/all-MiniLM-L6-v2"
        );
        _options.CacheDirectory = _modelCacheDirectory;

        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        // Clean up model cache directory if it exists
        try
        {
            if (Directory.Exists(_modelCacheDirectory))
            {
                Directory.Delete(_modelCacheDirectory, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }

        await Task.CompletedTask;
    }

    public string GetModelCacheDirectory() => _modelCacheDirectory;

    public LocalEmbeddingsOptions GetDefaultOptions()
    {
        return new LocalEmbeddingsOptions
        {
            ModelName = Options.ModelName,
            CacheDirectory = Options.CacheDirectory
        };
    }

    public LocalEmbeddingsOptions GetQuantizedOptions(bool preferQuantized = true)
    {
        var options = GetDefaultOptions();
        options.PreferQuantized = preferQuantized;
        return options;
    }
}
