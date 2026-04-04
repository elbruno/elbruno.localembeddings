using ElBruno.LocalEmbeddings.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ElBruno.LocalEmbeddings.Extensions;

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
    /// <para>
    /// <strong>Async-Safety Note:</strong> When <see cref="LocalEmbeddingsOptions.EnsureModelDownloaded"/>
    /// is <see langword="true"/> (the default), the DI factory resolves the generator by calling the
    /// <see cref="LocalEmbeddingGenerator"/> constructor, which performs a synchronous model download
    /// on the first service resolution. This is acceptable during host startup for most application
    /// types (worker services, console apps, ASP.NET Core apps where the first request resolves DI),
    /// but it can cause thread-pool starvation in hot-path or UI contexts.
    /// </para>
    /// <para>
    /// For fully non-blocking initialization, call
    /// <see cref="LocalEmbeddingGenerator.CreateAsync(LocalEmbeddingsOptions, CancellationToken)"/>
    /// before building the host and register the pre-built instance as a singleton directly:
    /// </para>
    /// </remarks>
    /// <example>
    /// Standard DI registration (synchronous download on first resolve):
    /// <code>
    /// services.AddLocalEmbeddings(options =>
    /// {
    ///     options.ModelName = "sentence-transformers/all-MiniLM-L6-v2";
    ///     options.MaxSequenceLength = 256;
    /// });
    /// </code>
    /// Fully async DI initialization — no thread-pool blocking:
    /// <code>
    /// // In Program.cs, before builder.Build():
    /// var generator = await LocalEmbeddingGenerator.CreateAsync(new LocalEmbeddingsOptions
    /// {
    ///     ModelName = "sentence-transformers/all-MiniLM-L6-v2"
    /// });
    /// builder.Services.AddSingleton&lt;IEmbeddingGenerator&lt;string, Embedding&lt;float&gt;&gt;&gt;(generator);
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
                o.UseParallelExecution = options.UseParallelExecution;
                o.PreferQuantized = options.PreferQuantized;
                o.InterOpNumThreads = options.InterOpNumThreads;
                o.IntraOpNumThreads = options.IntraOpNumThreads;
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

        // Register the embedding generator as a singleton.
        // NOTE: The factory calls the LocalEmbeddingGenerator constructor, which performs
        // a synchronous model download when EnsureModelDownloaded=true (sync-over-async).
        // This blocks the resolving thread on first resolution. If non-blocking startup is
        // required, use LocalEmbeddingGenerator.CreateAsync() before host build and register
        // the pre-built instance with AddSingleton(generator) instead.
        services.TryAddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LocalEmbeddingsOptions>>().Value;
            return new LocalEmbeddingGenerator(options);
        });

        return services;
    }

    /// <summary>
    /// Adds <see cref="LocalEmbeddingGenerator"/> with LRU caching to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureEmbeddings">An optional action to configure embedding options.</param>
    /// <param name="configureCache">An optional action to configure cache options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method registers a <see cref="CachingEmbeddingDecorator"/> that wraps the
    /// <see cref="LocalEmbeddingGenerator"/>. The cache stores embeddings keyed by the
    /// SHA-256 hash of the input text and uses an LRU eviction policy.
    /// </para>
    /// <para>
    /// Caching is particularly beneficial when the same texts are embedded repeatedly,
    /// such as in repeated searches or when processing documents with overlapping content.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddLocalEmbeddingsWithCache(
    ///     configureEmbeddings: options =>
    ///     {
    ///         options.ModelName = "sentence-transformers/all-MiniLM-L6-v2";
    ///     },
    ///     configureCache: options =>
    ///     {
    ///         options.Enabled = true;
    ///         options.MaxSize = 5000;
    ///     });
    /// </code>
    /// </example>
    public static IServiceCollection AddLocalEmbeddingsWithCache(
        this IServiceCollection services,
        Action<LocalEmbeddingsOptions>? configureEmbeddings = null,
        Action<EmbeddingCacheOptions>? configureCache = null)
    {
        services.AddOptions<EmbeddingCacheOptions>();
        if (configureCache is not null)
        {
            services.Configure(configureCache);
        }

        var optionsBuilder = services.AddOptions<LocalEmbeddingsOptions>();
        if (configureEmbeddings is not null)
        {
            optionsBuilder.Configure(configureEmbeddings);
        }

        services.AddHttpClient<IModelDownloader, ModelDownloader>()
            .ConfigureHttpClient(client =>
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("LocalEmbeddings/1.0");
            });

        services.TryAddSingleton<LocalEmbeddingGenerator>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<LocalEmbeddingsOptions>>().Value;
            return new LocalEmbeddingGenerator(options);
        });

        services.TryAddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            var innerGenerator = sp.GetRequiredService<LocalEmbeddingGenerator>();
            var cacheOptions = sp.GetRequiredService<IOptions<EmbeddingCacheOptions>>().Value;

            if (cacheOptions.Enabled)
            {
                return new CachingEmbeddingDecorator(innerGenerator, cacheOptions.MaxSize);
            }

            return innerGenerator;
        });

        return services;
    }
}
