using elbruno.LocalEmbeddings.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace elbruno.LocalEmbeddings.Extensions;

/// <summary>
/// Extension methods for registering LocalEmbeddings services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// The default configuration section name for LocalEmbeddings options.
    /// </summary>
    public const string DefaultConfigurationSectionName = "LocalEmbeddings";

    /// <summary>
    /// Adds <see cref="LocalEmbeddingGenerator"/> to the service collection using the Options pattern.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">An optional action to configure the options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// This overload registers <see cref="LocalEmbeddingsOptions"/> with the Options pattern,
    /// allowing configuration to be bound from <see cref="IConfiguration"/> sources.
    /// </para>
    /// <para>
    /// The <see cref="IModelDownloader"/> is registered using <see cref="IHttpClientFactory"/>
    /// for proper HttpClient lifecycle management.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddLocalEmbeddings(options =>
    /// {
    ///     options.ModelName = "sentence-transformers/all-MiniLM-L6-v2";
    ///     options.MaxSequenceLength = 256;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddLocalEmbeddings(
        this IServiceCollection services,
        Action<LocalEmbeddingsOptions>? configure = null)
    {
        // Register options with the Options pattern
        var optionsBuilder = services.AddOptions<LocalEmbeddingsOptions>();

        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        return services.AddLocalEmbeddingsCore();
    }

    /// <summary>
    /// Adds <see cref="LocalEmbeddingGenerator"/> to the service collection using a pre-configured options instance.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The pre-configured options instance.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    /// <remarks>
    /// Use this overload when you have a fully configured <see cref="LocalEmbeddingsOptions"/> instance
    /// and want to register it directly without additional configuration.
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = new LocalEmbeddingsOptions
    /// {
    ///     ModelName = "sentence-transformers/all-MiniLM-L6-v2",
    ///     MaxSequenceLength = 256
    /// };
    /// services.AddLocalEmbeddings(options);
    /// </code>
    /// </example>
    public static IServiceCollection AddLocalEmbeddings(
        this IServiceCollection services,
        LocalEmbeddingsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Register the options instance directly
        services.AddOptions<LocalEmbeddingsOptions>()
            .Configure(o =>
            {
                o.ModelName = options.ModelName;
                o.ModelPath = options.ModelPath;
                o.CacheDirectory = options.CacheDirectory;
                o.MaxSequenceLength = options.MaxSequenceLength;
                o.EnsureModelDownloaded = options.EnsureModelDownloaded;
                o.NormalizeEmbeddings = options.NormalizeEmbeddings;
            });

        return services.AddLocalEmbeddingsCore();
    }

    /// <summary>
    /// Adds <see cref="LocalEmbeddingGenerator"/> to the service collection using the specified model name.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="modelName">The HuggingFace model name (e.g., "sentence-transformers/all-MiniLM-L6-v2").</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="modelName"/> is null or whitespace.</exception>
    /// <remarks>
    /// <para>
    /// This is a convenience overload for quick setup when you only need to specify the model name.
    /// All other options use their default values.
    /// </para>
    /// <para>
    /// The model will be automatically downloaded and cached on first use.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddLocalEmbeddings("sentence-transformers/all-MiniLM-L6-v2");
    /// </code>
    /// </example>
    public static IServiceCollection AddLocalEmbeddings(
        this IServiceCollection services,
        string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            throw new ArgumentException("Model name cannot be null or whitespace.", nameof(modelName));
        }

        return services.AddLocalEmbeddings(options => options.ModelName = modelName);
    }

    /// <summary>
    /// Adds <see cref="LocalEmbeddingGenerator"/> to the service collection, binding options from configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration section to bind options from.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configuration"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// This overload binds <see cref="LocalEmbeddingsOptions"/> from an <see cref="IConfiguration"/> section,
    /// enabling configuration via appsettings.json, environment variables, or other configuration providers.
    /// </para>
    /// </remarks>
    /// <example>
    /// Configuration in appsettings.json:
    /// <code>
    /// {
    ///   "LocalEmbeddings": {
    ///     "ModelName": "sentence-transformers/all-MiniLM-L6-v2",
    ///     "MaxSequenceLength": 256,
    ///     "CacheDirectory": "/path/to/cache"
    ///   }
    /// }
    /// </code>
    /// Registration:
    /// <code>
    /// services.AddLocalEmbeddings(configuration.GetSection("LocalEmbeddings"));
    /// </code>
    /// </example>
    public static IServiceCollection AddLocalEmbeddings(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<LocalEmbeddingsOptions>()
            .Bind(configuration);

        return services.AddLocalEmbeddingsCore();
    }

    /// <summary>
    /// Registers core LocalEmbeddings services including HttpClient and the embedding generator.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    private static IServiceCollection AddLocalEmbeddingsCore(this IServiceCollection services)
    {
        // Register HttpClient for ModelDownloader using IHttpClientFactory
        services.AddHttpClient<IModelDownloader, ModelDownloader>()
            .ConfigureHttpClient(client =>
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("LocalEmbeddings/1.0");
            });

        // Register the embedding generator as a singleton
        // It resolves IOptions<LocalEmbeddingsOptions> and IModelDownloader from the container
        services.TryAddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LocalEmbeddingsOptions>>().Value;
            return new LocalEmbeddingGenerator(options);
        });

        return services;
    }
}
