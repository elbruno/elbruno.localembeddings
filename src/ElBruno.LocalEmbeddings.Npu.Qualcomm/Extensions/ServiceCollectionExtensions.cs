using ElBruno.LocalEmbeddings.Npu.Qualcomm.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ElBruno.LocalEmbeddings.Npu.Qualcomm.Extensions;

/// <summary>
/// Extension methods for registering Qualcomm NPU-accelerated LocalEmbeddings services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// The default configuration section name for Qualcomm NPU LocalEmbeddings options.
    /// </summary>
    public const string DefaultConfigurationSectionName = "LocalEmbeddingsNpuQualcomm";

    /// <summary>
    /// Adds <see cref="QualcommEmbeddingGenerator"/> to the service collection using the Options pattern.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">An optional action to configure the options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLocalEmbeddingsNpuQualcomm(
        this IServiceCollection services,
        Action<QualcommEmbeddingsOptions>? configure = null)
    {
        var optionsBuilder = services.AddOptions<QualcommEmbeddingsOptions>();

        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        return services.AddLocalEmbeddingsNpuQualcommCore();
    }

    /// <summary>
    /// Adds <see cref="QualcommEmbeddingGenerator"/> to the service collection using a pre-configured options instance.
    /// </summary>
    public static IServiceCollection AddLocalEmbeddingsNpuQualcomm(
        this IServiceCollection services,
        QualcommEmbeddingsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        services.AddOptions<QualcommEmbeddingsOptions>()
            .Configure(o =>
            {
                o.ModelName = options.ModelName;
                o.ModelPath = options.ModelPath;
                o.CacheDirectory = options.CacheDirectory;
                o.MaxSequenceLength = options.MaxSequenceLength;
                o.EnsureModelDownloaded = options.EnsureModelDownloaded;
                o.NormalizeEmbeddings = options.NormalizeEmbeddings;
                o.PreferQuantized = options.PreferQuantized;
                o.QnnBackendPath = options.QnnBackendPath;
                o.FallbackToCpu = options.FallbackToCpu;
            });

        return services.AddLocalEmbeddingsNpuQualcommCore();
    }

    /// <summary>
    /// Adds <see cref="QualcommEmbeddingGenerator"/> to the service collection using the specified model name.
    /// </summary>
    public static IServiceCollection AddLocalEmbeddingsNpuQualcomm(
        this IServiceCollection services,
        string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            throw new ArgumentException("Model name cannot be null or whitespace.", nameof(modelName));
        }

        return services.AddLocalEmbeddingsNpuQualcomm(options => options.ModelName = modelName);
    }

    /// <summary>
    /// Adds <see cref="QualcommEmbeddingGenerator"/> to the service collection, binding options from configuration.
    /// </summary>
    public static IServiceCollection AddLocalEmbeddingsNpuQualcomm(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<QualcommEmbeddingsOptions>()
            .Bind(configuration);

        return services.AddLocalEmbeddingsNpuQualcommCore();
    }

    private static IServiceCollection AddLocalEmbeddingsNpuQualcommCore(this IServiceCollection services)
    {
        services.TryAddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<QualcommEmbeddingsOptions>>().Value;
            return new QualcommEmbeddingGenerator(options);
        });

        return services;
    }
}
