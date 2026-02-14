using ElBruno.LocalEmbeddings.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.VectorData;

namespace ElBruno.LocalEmbeddings.VectorData.Extensions;

/// <summary>
/// Extension methods for registering LocalEmbeddings and VectorData services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds LocalEmbeddings and a <see cref="VectorStore"/> to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="vectorStoreFactory">The factory used to create the vector store implementation.</param>
    /// <param name="configure">An optional action to configure LocalEmbeddings options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLocalEmbeddingsWithVectorStore(
        this IServiceCollection services,
        Func<IServiceProvider, VectorStore> vectorStoreFactory,
        Action<LocalEmbeddingsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(vectorStoreFactory);

        ElBruno.LocalEmbeddings.Extensions.ServiceCollectionExtensions.AddLocalEmbeddings(services, configure);
        services.TryAddSingleton(vectorStoreFactory);

        return services;
    }

    /// <summary>
    /// Adds LocalEmbeddings and a <see cref="VectorStore"/> to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="vectorStoreFactory">The factory used to create the vector store implementation.</param>
    /// <param name="options">The pre-configured LocalEmbeddings options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLocalEmbeddingsWithVectorStore(
        this IServiceCollection services,
        Func<IServiceProvider, VectorStore> vectorStoreFactory,
        LocalEmbeddingsOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(vectorStoreFactory);
        ArgumentNullException.ThrowIfNull(options);

        ElBruno.LocalEmbeddings.Extensions.ServiceCollectionExtensions.AddLocalEmbeddings(services, options);
        services.TryAddSingleton(vectorStoreFactory);

        return services;
    }

    /// <summary>
    /// Adds LocalEmbeddings and a <see cref="VectorStore"/> to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="vectorStoreFactory">The factory used to create the vector store implementation.</param>
    /// <param name="configuration">The configuration section to bind LocalEmbeddings options from.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLocalEmbeddingsWithVectorStore(
        this IServiceCollection services,
        Func<IServiceProvider, VectorStore> vectorStoreFactory,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(vectorStoreFactory);
        ArgumentNullException.ThrowIfNull(configuration);

        ElBruno.LocalEmbeddings.Extensions.ServiceCollectionExtensions.AddLocalEmbeddings(services, configuration);
        services.TryAddSingleton(vectorStoreFactory);

        return services;
    }

    /// <summary>
    /// Registers a typed vector collection from the configured <see cref="VectorStore"/>.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TRecord">The record type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="collectionName">The collection name in the vector store.</param>
    /// <param name="definition">An optional collection definition.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="collectionName"/> is null or whitespace.</exception>
    public static IServiceCollection AddVectorStoreCollection<TKey, TRecord>(
        this IServiceCollection services,
        string collectionName,
        VectorStoreCollectionDefinition? definition = null)
        where TKey : notnull
        where TRecord : class
    {
        ArgumentNullException.ThrowIfNull(services);

        if (string.IsNullOrWhiteSpace(collectionName))
        {
            throw new ArgumentException("Collection name cannot be null or whitespace.", nameof(collectionName));
        }

        services.TryAddSingleton(sp =>
            sp.GetRequiredService<VectorStore>().GetCollection<TKey, TRecord>(
                collectionName,
                definition ?? new VectorStoreCollectionDefinition()));

        return services;
    }
}
