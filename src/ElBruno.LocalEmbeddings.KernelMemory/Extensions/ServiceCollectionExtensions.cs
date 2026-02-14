using ElBruno.LocalEmbeddings.Extensions;
using ElBruno.LocalEmbeddings.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.KernelMemory.AI;

namespace ElBruno.LocalEmbeddings.KernelMemory.Extensions;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to register both M.E.AI
/// and Kernel Memory embedding services from <c>ElBruno.LocalEmbeddings</c>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="LocalEmbeddingGenerator"/> and a Kernel Memory
    /// <see cref="ITextEmbeddingGenerator"/> adapter to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">An optional action to configure <see cref="LocalEmbeddingsOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method first calls <see cref="LocalEmbeddings.Extensions.ServiceCollectionExtensions.AddLocalEmbeddings(IServiceCollection, Action{LocalEmbeddingsOptions}?)"/>
    /// to register the M.E.AI <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> singleton,
    /// then registers <see cref="ITextEmbeddingGenerator"/> as a singleton that wraps the
    /// M.E.AI generator with <see cref="LocalEmbeddingTextGenerator"/>.
    /// </para>
    /// <para>
    /// After calling this method, both interfaces resolve from the container:
    /// <list type="bullet">
    ///   <item><see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> — for M.E.AI consumers</item>
    ///   <item><see cref="ITextEmbeddingGenerator"/> — for Kernel Memory consumers</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddLocalEmbeddingsWithKernelMemory(options =>
    /// {
    ///     options.ModelName = "sentence-transformers/all-MiniLM-L6-v2";
    /// });
    /// 
    /// // Both interfaces resolve from the container:
    /// // - IEmbeddingGenerator&lt;string, Embedding&lt;float&gt;&gt;
    /// // - ITextEmbeddingGenerator
    /// </code>
    /// </example>
    public static IServiceCollection AddLocalEmbeddingsWithKernelMemory(
        this IServiceCollection services,
        Action<LocalEmbeddingsOptions>? configure = null)
    {
        // Register the core M.E.AI embedding generator
        services.AddLocalEmbeddings(configure);

        // Register the Kernel Memory adapter on top
        return services.AddKernelMemoryEmbeddingAdapter();
    }

    /// <summary>
    /// Adds <see cref="LocalEmbeddingGenerator"/> and a Kernel Memory
    /// <see cref="ITextEmbeddingGenerator"/> adapter using a pre-configured options instance.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The pre-configured options instance.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    public static IServiceCollection AddLocalEmbeddingsWithKernelMemory(
        this IServiceCollection services,
        LocalEmbeddingsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        services.AddLocalEmbeddings(options);
        return services.AddKernelMemoryEmbeddingAdapter(options.MaxSequenceLength);
    }

    /// <summary>
    /// Adds <see cref="LocalEmbeddingGenerator"/> and a Kernel Memory
    /// <see cref="ITextEmbeddingGenerator"/> adapter, binding options from configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration section to bind options from.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configuration"/> is null.</exception>
    /// <example>
    /// <code>
    /// // appsettings.json:
    /// // { "LocalEmbeddings": { "ModelName": "sentence-transformers/all-MiniLM-L6-v2" } }
    /// 
    /// services.AddLocalEmbeddingsWithKernelMemory(
    ///     configuration.GetSection("LocalEmbeddings"));
    /// </code>
    /// </example>
    public static IServiceCollection AddLocalEmbeddingsWithKernelMemory(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddLocalEmbeddings(configuration);
        return services.AddKernelMemoryEmbeddingAdapter();
    }

    /// <summary>
    /// Registers the <see cref="ITextEmbeddingGenerator"/> adapter that wraps the
    /// already-registered <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/>.
    /// </summary>
    private static IServiceCollection AddKernelMemoryEmbeddingAdapter(
        this IServiceCollection services,
        int maxTokens = 512)
    {
        services.TryAddSingleton<ITextEmbeddingGenerator>(sp =>
        {
            var generator = sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
            return new LocalEmbeddingTextGenerator(generator, maxTokens: maxTokens);
        });

        return services;
    }
}
