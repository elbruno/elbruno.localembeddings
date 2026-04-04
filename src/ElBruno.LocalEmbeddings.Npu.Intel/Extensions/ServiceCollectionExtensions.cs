using ElBruno.LocalEmbeddings.Npu.Intel.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ElBruno.LocalEmbeddings.Npu.Intel.Extensions;

/// <summary>
/// Extension methods for registering Intel NPU-accelerated LocalEmbeddings services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// The default configuration section name for Intel NPU LocalEmbeddings options.
    /// </summary>
    public const string DefaultConfigurationSectionName = "LocalEmbeddingsNpuIntel";

    /// <summary>
    /// Adds <see cref="IntelEmbeddingGenerator"/> to the service collection using the Options pattern.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">An optional action to configure the options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLocalEmbeddingsNpuIntel(
        this IServiceCollection services,
        Action<IntelEmbeddingsOptions>? configure = null)
    {
        var optionsBuilder = services.AddOptions<IntelEmbeddingsOptions>();

        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        return services.AddLocalEmbeddingsNpuIntelCore();
    }

    /// <summary>
    /// Adds <see cref="IntelEmbeddingGenerator"/> to the service collection using a pre-configured options instance.
    /// </summary>
    public static IServiceCollection AddLocalEmbeddingsNpuIntel(
        this IServiceCollection services,
        IntelEmbeddingsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        services.AddOptions<IntelEmbeddingsOptions>()
            .Configure(o =>
            {
                o.ModelName = options.ModelName;
                o.ModelPath = options.ModelPath;
                o.CacheDirectory = options.CacheDirectory;
                o.MaxSequenceLength = options.MaxSequenceLength;
                o.EnsureModelDownloaded = options.EnsureModelDownloaded;
                o.NormalizeEmbeddings = options.NormalizeEmbeddings;
                o.PreferQuantized = options.PreferQuantized;
                o.DeviceType = options.DeviceType;
                o.FallbackToCpu = options.FallbackToCpu;
            });

        return services.AddLocalEmbeddingsNpuIntelCore();
    }

    /// <summary>
    /// Adds <see cref="IntelEmbeddingGenerator"/> to the service collection using the specified model name.
    /// </summary>
    public static IServiceCollection AddLocalEmbeddingsNpuIntel(
        this IServiceCollection services,
        string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            throw new ArgumentException("Model name cannot be null or whitespace.", nameof(modelName));
        }

        return services.AddLocalEmbeddingsNpuIntel(options => options.ModelName = modelName);
    }

    /// <summary>
    /// Adds <see cref="IntelEmbeddingGenerator"/> to the service collection, binding options from configuration.
    /// </summary>
    public static IServiceCollection AddLocalEmbeddingsNpuIntel(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<IntelEmbeddingsOptions>()
            .Bind(configuration);

        return services.AddLocalEmbeddingsNpuIntelCore();
    }

    private static IServiceCollection AddLocalEmbeddingsNpuIntelCore(this IServiceCollection services)
    {
        services.TryAddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<IntelEmbeddingsOptions>>().Value;
            return new IntelEmbeddingGenerator(options);
        });

        return services;
    }
}
