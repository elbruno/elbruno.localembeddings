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

        services.TryAddSingleton(new ClipImageEncoder(options.VisionModelPath));
        services.TryAddSingleton(new ClipTextEncoder(options.TextModelPath, options.VocabPath, options.MergesPath));
        services.TryAddSingleton<ImageSearchEngine>();

        return services;
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

        services.TryAddSingleton(new ClipImageEncoder(options.VisionModelPath));
        services.TryAddSingleton(new ClipTextEncoder(options.TextModelPath, options.VocabPath, options.MergesPath));
        services.TryAddSingleton<ImageSearchEngine>();

        return services;
    }
}
