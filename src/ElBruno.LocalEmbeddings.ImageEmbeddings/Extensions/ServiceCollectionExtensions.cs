using ElBruno.LocalEmbeddings.ImageEmbeddings.Downloader;
using ElBruno.LocalEmbeddings.ImageEmbeddings.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ElBruno.LocalEmbeddings.ImageEmbeddings.Extensions;

/// <summary>
/// Extension methods for registering image embedding services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds CLIP-based image embedding services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">An action to configure the image embeddings options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
    /// <remarks>
    /// Registers <see cref="ClipImageEncoder"/>, <see cref="ClipTextEncoder"/>,
    /// and <see cref="ImageSearchEngine"/> as singletons in the service collection.
    /// The <see cref="ImageEmbeddingsOptions.ModelDirectory"/> must point to a directory
    /// containing the CLIP ONNX model files.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddImageEmbeddings(options =>
    /// {
    ///     options.ModelDirectory = "/path/to/clip-models";
    ///     options.EnsureModelDownloaded = true;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddImageEmbeddings(
        this IServiceCollection services,
        Action<ImageEmbeddingsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new ImageEmbeddingsOptions();
        configure(options);

        return services.AddImageEmbeddings(options);
    }

    /// <summary>
    /// Adds CLIP-based image embedding services to the service collection using
    /// a pre-configured options instance.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The pre-configured options instance.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    public static IServiceCollection AddImageEmbeddings(
        this IServiceCollection services,
        ImageEmbeddingsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.EnsureModelDownloaded)
        {
            services.AddImageModelDownloader();
        }

        services.TryAddSingleton(sp =>
        {
            EnsureModels(sp, options);
            return new ClipImageEncoder(options.VisionModelPath);
        });

        services.TryAddSingleton(sp =>
        {
            EnsureModels(sp, options);
            return new ClipTextEncoder(options.TextModelPath, options.VocabPath, options.MergesPath);
        });

        services.TryAddSingleton<ImageSearchEngine>();

        return services;
    }

    private static void EnsureModels(IServiceProvider services, ImageEmbeddingsOptions options)
    {
        if (options.EnsureModelDownloaded)
        {
            var downloader = services.GetRequiredService<IImageModelDownloader>();
            // Synchronously ensure models are downloaded to prevent startup errors
            downloader.EnsureModelDownloadedAsync(options.ModelDirectory).GetAwaiter().GetResult();
        }
        else
        {
            // Verify files exist, else throw friendly error
            var requiredFiles = new[]
            {
                (options.TextModelPath, "Text Model"),
                (options.VisionModelPath, "Vision Model"),
                (options.VocabPath, "Vocab File"),
                (options.MergesPath, "Merges File")
            };

            var missingFiles = requiredFiles.Where(f => !File.Exists(f.Item1)).ToList();
            if (missingFiles.Count > 0)
            {
                var missingFileNames = string.Join(", ", missingFiles.Select(f => f.Item2));
                throw new InvalidOperationException(
                    $"Missing required model files: {missingFileNames}. " +
                    $"Please ensure they exist in '{options.ModelDirectory}' or set 'EnsureModelDownloaded' to true.");
            }
        }
    }
}
