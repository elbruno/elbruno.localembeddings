using System.ClientModel;
using ElBruno.LocalEmbeddings.Azure.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;

namespace ElBruno.LocalEmbeddings.Azure.Extensions;

/// <summary>
/// Extension methods for configuring local embeddings with Azure OpenAI fallback.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers local embeddings with Azure OpenAI fallback as a singleton service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure the hybrid embedding options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// This extension registers the <see cref="HybridAzureEmbeddingGenerator"/> which tries
    /// to generate embeddings locally first, then falls back to Azure OpenAI if the local
    /// generation fails.
    /// </para>
    /// <para>
    /// The local embedding generator must already be registered in the service collection
    /// before calling this method.
    /// </para>
    /// <para>
    /// Example:
    /// <code>
    /// services.AddLocalEmbeddings()
    ///     .AddLocalEmbeddingsWithAzureFallback(options =>
    ///     {
    ///         options.Endpoint = "https://my-resource.openai.azure.com";
    ///         options.ApiKey = "your-api-key";
    ///         options.DeploymentName = "text-embedding-3-small";
    ///     });
    /// </code>
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when services or configure is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when options are invalid.</exception>
    public static IServiceCollection AddLocalEmbeddingsWithAzureFallback(
        this IServiceCollection services,
        Action<LocalEmbeddingsAzureOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<LocalEmbeddingsAzureOptions>()
            .Configure(configure)
            .ValidateOnStart();

        return AddHybridEmbeddingGenerator(services);
    }

    /// <summary>
    /// Registers local embeddings with Azure OpenAI fallback using configuration from appsettings.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configurationSectionName">The configuration section name (default: "AzureEmbeddings").</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This overload reads configuration from IConfiguration. Ensure your appsettings.json
    /// has the appropriate section:
    /// <code>
    /// {
    ///   "AzureEmbeddings": {
    ///     "Endpoint": "https://my-resource.openai.azure.com",
    ///     "ApiKey": "your-api-key",
    ///     "DeploymentName": "text-embedding-3-small"
    ///   }
    /// }
    /// </code>
    /// </remarks>
    public static IServiceCollection AddLocalEmbeddingsWithAzureFallback(
        this IServiceCollection services,
        string configurationSectionName = "AzureEmbeddings")
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<LocalEmbeddingsAzureOptions>()
            .BindConfiguration(configurationSectionName)
            .ValidateOnStart();

        return AddHybridEmbeddingGenerator(services);
    }

    private static IServiceCollection AddHybridEmbeddingGenerator(IServiceCollection services)
    {
        var existingDescriptor = services.FirstOrDefault(
            sd => sd.ServiceType == typeof(IEmbeddingGenerator<string, Embedding<float>>));
        if (existingDescriptor is null)
        {
            throw new InvalidOperationException(
                "No IEmbeddingGenerator<string, Embedding<float>> is registered. " +
                "Call AddLocalEmbeddings() before AddLocalEmbeddingsWithAzureFallback().");
        }

        services.Remove(existingDescriptor);

        services.Add(ServiceDescriptor.Describe(
            typeof(IEmbeddingGenerator<string, Embedding<float>>),
            provider =>
            {
                IEmbeddingGenerator<string, Embedding<float>> localGenerator;

                if (existingDescriptor.ImplementationFactory is not null)
                {
                    localGenerator = (IEmbeddingGenerator<string, Embedding<float>>)existingDescriptor.ImplementationFactory(provider)!;
                }
                else if (existingDescriptor.ImplementationInstance is not null)
                {
                    localGenerator = (IEmbeddingGenerator<string, Embedding<float>>)existingDescriptor.ImplementationInstance;
                }
                else
                {
                    var implementationType = existingDescriptor.ImplementationType ??
                        typeof(IEmbeddingGenerator<string, Embedding<float>>);
                    localGenerator = (IEmbeddingGenerator<string, Embedding<float>>)ActivatorUtilities.GetServiceOrCreateInstance(
                        provider,
                        implementationType)!;
                }

            var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<LocalEmbeddingsAzureOptions>>();
            var options = optionsMonitor.CurrentValue;
            var logger = provider.GetService<ILoggerFactory>()?.CreateLogger<HybridAzureEmbeddingGenerator>();

            var validationErrors = options.Validate();
            if (validationErrors.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Invalid Azure OpenAI configuration: {string.Join(", ", validationErrors)}");
            }

            var azureClient = new OpenAIClient(
                new ApiKeyCredential(options.ApiKey!),
                new OpenAIClientOptions { Endpoint = new Uri(options.Endpoint!) });

            return new HybridAzureEmbeddingGenerator(localGenerator, azureClient, options, logger);
            },
            existingDescriptor.Lifetime));

        return services;
    }
}
