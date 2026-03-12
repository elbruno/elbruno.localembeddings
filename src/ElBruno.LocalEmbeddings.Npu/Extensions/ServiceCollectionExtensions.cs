using ElBruno.LocalEmbeddings.Npu.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ElBruno.LocalEmbeddings.Npu.Extensions;

/// <summary>
/// Extension methods for registering NPU-accelerated LocalEmbeddings services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// The default configuration section name for NPU LocalEmbeddings options.
    /// </summary>
    public const string DefaultConfigurationSectionName = "LocalEmbeddingsNpu";

    /// <summary>
    /// Adds <see cref="NpuEmbeddingGenerator"/> to the service collection using the Options pattern.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">An optional action to configure the options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddLocalEmbeddingsNpu(options =>
    /// {
    ///     options.ModelName = "sentence-transformers/all-MiniLM-L6-v2";
    ///     options.DeviceId = 0;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddLocalEmbeddingsNpu(
        this IServiceCollection services,
        Action<NpuEmbeddingsOptions>? configure = null)
    {
        var optionsBuilder = services.AddOptions<NpuEmbeddingsOptions>();

        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        return services.AddLocalEmbeddingsNpuCore();
    }

    /// <summary>
    /// Adds <see cref="NpuEmbeddingGenerator"/> to the service collection using a pre-configured options instance.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The pre-configured options instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLocalEmbeddingsNpu(
        this IServiceCollection services,
        NpuEmbeddingsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        services.AddOptions<NpuEmbeddingsOptions>()
            .Configure(o =>
            {
                o.ModelName = options.ModelName;
                o.ModelPath = options.ModelPath;
                o.CacheDirectory = options.CacheDirectory;
                o.MaxSequenceLength = options.MaxSequenceLength;
                o.EnsureModelDownloaded = options.EnsureModelDownloaded;
                o.NormalizeEmbeddings = options.NormalizeEmbeddings;
                o.PreferQuantized = options.PreferQuantized;
                o.DeviceId = options.DeviceId;
            });

        return services.AddLocalEmbeddingsNpuCore();
    }

    /// <summary>
    /// Adds <see cref="NpuEmbeddingGenerator"/> to the service collection using the specified model name.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="modelName">The HuggingFace model name.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLocalEmbeddingsNpu(
        this IServiceCollection services,
        string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            throw new ArgumentException("Model name cannot be null or whitespace.", nameof(modelName));
        }

        return services.AddLocalEmbeddingsNpu(options => options.ModelName = modelName);
    }

    /// <summary>
    /// Adds <see cref="NpuEmbeddingGenerator"/> to the service collection, binding options from configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration section to bind options from.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLocalEmbeddingsNpu(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<NpuEmbeddingsOptions>()
            .Bind(configuration);

        return services.AddLocalEmbeddingsNpuCore();
    }

    private static IServiceCollection AddLocalEmbeddingsNpuCore(this IServiceCollection services)
    {
        services.TryAddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<NpuEmbeddingsOptions>>().Value;
            return new NpuEmbeddingGenerator(options);
        });

        return services;
    }
}
