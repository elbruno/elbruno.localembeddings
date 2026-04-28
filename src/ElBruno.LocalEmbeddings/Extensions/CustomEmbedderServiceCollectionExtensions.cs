using System.Diagnostics.CodeAnalysis;
using ElBruno.LocalEmbeddings.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ElBruno.LocalEmbeddings.Extensions;

/// <summary>
/// Extension methods for registering custom embedders with dependency injection.
/// </summary>
public static class CustomEmbedderServiceCollectionExtensions
{
    /// <summary>
    /// Adds a custom embedder implementation to the service collection as an <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/>.
    /// </summary>
    /// <typeparam name="TImplementation">
    /// The concrete type that implements <see cref="ICustomEmbedder"/>.
    /// Must have a public constructor that can be resolved by the service provider.
    /// </typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">An optional action to configure <see cref="CustomEmbedderOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method registers the custom embedder implementation as a singleton and wraps it
    /// with a <see cref="CustomEmbedderAdapter"/> to make it compatible with Microsoft.Extensions.AI.
    /// </para>
    /// <para>
    /// The embedder implementation (<typeparamref name="TImplementation"/>) must be constructible
    /// via dependency injection (i.e., its constructor parameters must be resolvable from the service provider).
    /// </para>
    /// <para>
    /// If your embedder implementation requires specific configuration or factory setup,
    /// register it manually using <see cref="ServiceCollectionServiceExtensions.AddSingleton{TService}(IServiceCollection, Func{IServiceProvider, TService})"/>
    /// and then call <see cref="CustomEmbedder.CreateAdapter"/> to wrap it.
    /// </para>
    /// </remarks>
    /// <example>
    /// Register a custom Ollama embedder:
    /// <code>
    /// services.AddCustomEmbedder&lt;OllamaEmbedder&gt;(options =>
    /// {
    ///     options.NormalizeEmbeddings = false; // Ollama already normalizes
    /// });
    /// </code>
    /// 
    /// Then inject and use:
    /// <code>
    /// public class MyService
    /// {
    ///     private readonly IEmbeddingGenerator&lt;string, Embedding&lt;float&gt;&gt; _embeddings;
    ///     
    ///     public MyService(IEmbeddingGenerator&lt;string, Embedding&lt;float&gt;&gt; embeddings)
    ///     {
    ///         _embeddings = embeddings;
    ///     }
    ///     
    ///     public async Task&lt;float[]&gt; GetEmbeddingAsync(string text)
    ///     {
    ///         var result = await _embeddings.GenerateAsync(new[] { text });
    ///         return result.First().Vector.ToArray();
    ///     }
    /// }
    /// </code>
    /// </example>
    public static IServiceCollection AddCustomEmbedder<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
        this IServiceCollection services,
        Action<CustomEmbedderOptions>? configure = null)
        where TImplementation : class, ICustomEmbedder
    {
        // Register options
        services.AddOptions<CustomEmbedderOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        // Register the custom embedder implementation as a singleton
        services.TryAddSingleton<TImplementation>();

        // Register the adapter as IEmbeddingGenerator
        services.TryAddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            var embedder = sp.GetRequiredService<TImplementation>();
            var options = sp.GetService<Microsoft.Extensions.Options.IOptions<CustomEmbedderOptions>>()?.Value
                ?? new CustomEmbedderOptions();
            
            return CustomEmbedder.CreateAdapter(embedder, embedder.Name, options);
        });

        return services;
    }

    /// <summary>
    /// Adds a custom embedder instance to the service collection as an <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="embedder">The pre-configured custom embedder instance.</param>
    /// <param name="options">Optional configuration options for the adapter.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="embedder"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// Use this overload when you have a pre-configured embedder instance (e.g., created with
    /// specific connection settings, API keys, etc.) that you want to register directly.
    /// </para>
    /// <para>
    /// The embedder is wrapped in a <see cref="CustomEmbedderAdapter"/> and registered as a singleton.
    /// </para>
    /// </remarks>
    /// <example>
    /// Register a pre-configured Ollama embedder:
    /// <code>
    /// var ollamaEmbedder = new OllamaEmbedder("http://localhost:11434", "nomic-embed-text");
    /// var options = new CustomEmbedderOptions { NormalizeEmbeddings = false };
    /// services.AddCustomEmbedder(ollamaEmbedder, options);
    /// </code>
    /// </example>
    public static IServiceCollection AddCustomEmbedder(
        this IServiceCollection services,
        ICustomEmbedder embedder,
        CustomEmbedderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(embedder);

        var generator = CustomEmbedder.CreateAdapter(
            embedder,
            embedder.Name,
            options ?? new CustomEmbedderOptions());

        services.TryAddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(generator);

        return services;
    }
}
