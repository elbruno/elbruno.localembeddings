using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ElBruno.LocalEmbeddings.ImageEmbeddings.Downloader;

/// <summary>
/// Extension methods for registering the image model downloader.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the HuggingFace image model downloader to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddImageModelDownloader(this IServiceCollection services)
    {
        services.AddHttpClient<IImageModelDownloader, HuggingFaceImageModelDownloader>();
        services.TryAddTransient<IImageModelDownloader, HuggingFaceImageModelDownloader>();
        return services;
    }
}