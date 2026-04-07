using ElBruno.LocalEmbeddings.Harrier.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ElBruno.LocalEmbeddings.Harrier.Extensions;

/// <summary>
/// Extension methods for registering Harrier embedding services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// The default configuration section name for Harrier embedding options.
    /// </summary>
    public const string DefaultConfigurationSectionName = "HarrierEmbeddings";

    /// <summary>
    /// Adds <see cref="HarrierEmbeddingGenerator"/> to the service collection using the Options pattern.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">An optional action to configure the options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Async-Safety Note:</strong> The DI factory performs a synchronous model download
    /// on first resolution when <see cref="HarrierEmbeddingsOptions.EnsureModelDownloaded"/> is true.
    /// For fully non-blocking initialization, use
    /// <see cref="HarrierEmbeddingGenerator.CreateAsync(HarrierEmbeddingsOptions, CancellationToken)"/>
    /// before building the host and register the pre-built instance as a singleton.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddHarrierEmbeddings(options =>
    /// {
    ///     options.InstructionPrefix = "Instruct: Retrieve semantically similar text\nQuery: ";
    ///     options.ModelVariant = HarrierModelVariant.Q4;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddHarrierEmbeddings(
        this IServiceCollection services,
        Action<HarrierEmbeddingsOptions>? configure = null)
    {
        var optionsBuilder = services.AddOptions<HarrierEmbeddingsOptions>();

        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        return services.AddHarrierEmbeddingsCore();
    }

    /// <summary>
    /// Adds <see cref="HarrierEmbeddingGenerator"/> to the service collection using a pre-configured options instance.
    /// </summary>
    public static IServiceCollection AddHarrierEmbeddings(
        this IServiceCollection services,
        HarrierEmbeddingsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        services.AddOptions<HarrierEmbeddingsOptions>()
            .Configure(o =>
            {
                o.ModelName = options.ModelName;
                o.ModelPath = options.ModelPath;
                o.CacheDirectory = options.CacheDirectory;
                o.MaxSequenceLength = options.MaxSequenceLength;
                o.EnsureModelDownloaded = options.EnsureModelDownloaded;
                o.UseParallelExecution = options.UseParallelExecution;
                o.ModelVariant = options.ModelVariant;
                o.InstructionPrefix = options.InstructionPrefix;
                o.InterOpNumThreads = options.InterOpNumThreads;
                o.IntraOpNumThreads = options.IntraOpNumThreads;
                o.ExpectedHash = options.ExpectedHash;
            });

        return services.AddHarrierEmbeddingsCore();
    }

    /// <summary>
    /// Adds <see cref="HarrierEmbeddingGenerator"/> to the service collection, binding options from configuration.
    /// </summary>
    public static IServiceCollection AddHarrierEmbeddings(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<HarrierEmbeddingsOptions>()
            .Bind(configuration);

        return services.AddHarrierEmbeddingsCore();
    }

    private static IServiceCollection AddHarrierEmbeddingsCore(this IServiceCollection services)
    {
        // Sync-over-async: safe in console/desktop apps. Use CreateAsync() in async contexts.
        services.TryAddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<HarrierEmbeddingsOptions>>().Value;
            return HarrierEmbeddingGenerator.CreateAsync(options).GetAwaiter().GetResult();
        });

        return services;
    }
}
