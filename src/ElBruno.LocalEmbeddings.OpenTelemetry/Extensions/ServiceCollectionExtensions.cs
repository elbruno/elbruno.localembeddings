using ElBruno.LocalEmbeddings.OpenTelemetry.Instrumentation;
using ElBruno.LocalEmbeddings.OpenTelemetry.Metrics;
using ElBruno.LocalEmbeddings.OpenTelemetry.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ElBruno.LocalEmbeddings.OpenTelemetry.Extensions;

/// <summary>
/// Extension methods for registering OpenTelemetry instrumentation with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds OpenTelemetry instrumentation to the embedding generator.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action for OpenTelemetry options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This method wraps the existing <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> with
    /// OpenTelemetry instrumentation, adding distributed tracing, metrics collection, and structured
    /// events. The core generator implementation remains unchanged.
    ///
    /// Example:
    /// <code>
    /// services
    ///     .AddLocalEmbeddings()
    ///     .AddLocalEmbeddingsOpenTelemetry(options =>
    ///     {
    ///         options.EnableTracing = true;
    ///         options.EnableMetrics = true;
    ///         options.SamplingRate = 0.1;
    ///     });
    /// </code>
    /// </remarks>
    public static IServiceCollection AddLocalEmbeddingsOpenTelemetry(
        this IServiceCollection services,
        Action<LocalEmbeddingsOpenTelemetryOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new LocalEmbeddingsOpenTelemetryOptions();
        if (configure is not null)
        {
            configure(options);
        }
        options.Validate();

        // Create MetricMeter if metrics are enabled and not provided
        if (options.EnableMetrics && options.MetricMeter is null)
        {
            options.MetricMeter = new MetricMeter();
        }

        services.AddSingleton(options);
        if (options.MetricMeter is not null)
        {
            services.AddSingleton(options.MetricMeter);
        }

        // Store the existing factory before we replace it
        var existingDescriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(IEmbeddingGenerator<string, Embedding<float>>));
        if (existingDescriptor is null)
        {
            throw new InvalidOperationException(
                "No IEmbeddingGenerator<string, Embedding<float>> is registered. " +
                "Call AddLocalEmbeddings() before AddLocalEmbeddingsOpenTelemetry().");
        }

        // Remove the existing descriptor so we can replace it
        services.Remove(existingDescriptor);

        // Add the instrumented version that wraps the original
        services.Add(ServiceDescriptor.Describe(
            typeof(IEmbeddingGenerator<string, Embedding<float>>),
            provider =>
            {
                // Create the inner generator using the original descriptor's factory/implementation
                IEmbeddingGenerator<string, Embedding<float>> innerGenerator;
                
                if (existingDescriptor.ImplementationFactory is not null)
                {
                    innerGenerator = (IEmbeddingGenerator<string, Embedding<float>>)existingDescriptor.ImplementationFactory(provider)!;
                }
                else if (existingDescriptor.ImplementationInstance is not null)
                {
                    innerGenerator = (IEmbeddingGenerator<string, Embedding<float>>)existingDescriptor.ImplementationInstance;
                }
                else
                {
                    var implementationType = existingDescriptor.ImplementationType ?? typeof(IEmbeddingGenerator<string, Embedding<float>>);
                    innerGenerator = (IEmbeddingGenerator<string, Embedding<float>>)ActivatorUtilities.GetServiceOrCreateInstance(provider, implementationType)!;
                }

                var opts = provider.GetRequiredService<LocalEmbeddingsOpenTelemetryOptions>();
                var logger = provider.GetService<Microsoft.Extensions.Logging.ILogger<InstrumentedEmbeddingGenerator>>();
                
                return new InstrumentedEmbeddingGenerator(innerGenerator, opts, logger);
            },
            existingDescriptor.Lifetime));

        return services;
    }

    /// <summary>
    /// Adds OpenTelemetry instrumentation with configuration from IConfiguration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration section for OpenTelemetry options.</param>
    /// <param name="configure">Optional additional configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This overload binds OpenTelemetry options from the configuration section "OpenTelemetry"
    /// by default, or a custom section specified in the <paramref name="configuration"/> parameter.
    ///
    /// Example in appsettings.json:
    /// <code>
    /// {
    ///   "OpenTelemetry": {
    ///     "EnableTracing": true,
    ///     "EnableMetrics": true,
    ///     "SamplingRate": 0.5
    ///   }
    /// }
    /// </code>
    /// </remarks>
    public static IServiceCollection AddLocalEmbeddingsOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<LocalEmbeddingsOpenTelemetryOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<LocalEmbeddingsOpenTelemetryOptions>()
            .Bind(configuration.GetSection("OpenTelemetry"))
            .ValidateOnStart();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        // Store the existing factory before we replace it
        var existingDescriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(IEmbeddingGenerator<string, Embedding<float>>));
        if (existingDescriptor is null)
        {
            throw new InvalidOperationException(
                "No IEmbeddingGenerator<string, Embedding<float>> is registered. " +
                "Call AddLocalEmbeddings() before AddLocalEmbeddingsOpenTelemetry().");
        }

        // Add MetricMeter as singleton
        services.AddSingleton<MetricMeter>();

        // Remove the existing descriptor so we can replace it
        services.Remove(existingDescriptor);

        // Add the instrumented version that wraps the original
        services.Add(ServiceDescriptor.Describe(
            typeof(IEmbeddingGenerator<string, Embedding<float>>),
            provider =>
            {
                // Create the inner generator using the original descriptor's factory/implementation
                IEmbeddingGenerator<string, Embedding<float>> innerGenerator;
                
                if (existingDescriptor.ImplementationFactory is not null)
                {
                    innerGenerator = (IEmbeddingGenerator<string, Embedding<float>>)existingDescriptor.ImplementationFactory(provider)!;
                }
                else if (existingDescriptor.ImplementationInstance is not null)
                {
                    innerGenerator = (IEmbeddingGenerator<string, Embedding<float>>)existingDescriptor.ImplementationInstance;
                }
                else
                {
                    var implementationType = existingDescriptor.ImplementationType ?? typeof(IEmbeddingGenerator<string, Embedding<float>>);
                    innerGenerator = (IEmbeddingGenerator<string, Embedding<float>>)ActivatorUtilities.GetServiceOrCreateInstance(provider, implementationType)!;
                }

                var options = provider.GetRequiredService<LocalEmbeddingsOpenTelemetryOptions>();
                var meter = provider.GetService<MetricMeter>();
                if (options.EnableMetrics && meter is not null)
                {
                    options.MetricMeter = meter;
                }

                var logger = provider.GetService<Microsoft.Extensions.Logging.ILogger<InstrumentedEmbeddingGenerator>>();
                
                return new InstrumentedEmbeddingGenerator(innerGenerator, options, logger);
            },
            existingDescriptor.Lifetime));

        return services;
    }

    /// <summary>
    /// Adds OpenTelemetry instrumentation with the provided options instance.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The pre-configured OpenTelemetry options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    public static IServiceCollection AddLocalEmbeddingsOpenTelemetry(
        this IServiceCollection services,
        LocalEmbeddingsOpenTelemetryOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        options.Validate();

        // Create MetricMeter if metrics are enabled and not provided
        if (options.EnableMetrics && options.MetricMeter is null)
        {
            options.MetricMeter = new MetricMeter();
        }

        services.AddSingleton(options);
        if (options.MetricMeter is not null)
        {
            services.AddSingleton(options.MetricMeter);
        }

        // Store the existing factory before we replace it
        var existingDescriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(IEmbeddingGenerator<string, Embedding<float>>));
        if (existingDescriptor is null)
        {
            throw new InvalidOperationException(
                "No IEmbeddingGenerator<string, Embedding<float>> is registered. " +
                "Call AddLocalEmbeddings() before AddLocalEmbeddingsOpenTelemetry().");
        }

        // Remove the existing descriptor so we can replace it
        services.Remove(existingDescriptor);

        // Add the instrumented version that wraps the original
        services.Add(ServiceDescriptor.Describe(
            typeof(IEmbeddingGenerator<string, Embedding<float>>),
            provider =>
            {
                // Create the inner generator using the original descriptor's factory/implementation
                IEmbeddingGenerator<string, Embedding<float>> innerGenerator;
                
                if (existingDescriptor.ImplementationFactory is not null)
                {
                    innerGenerator = (IEmbeddingGenerator<string, Embedding<float>>)existingDescriptor.ImplementationFactory(provider)!;
                }
                else if (existingDescriptor.ImplementationInstance is not null)
                {
                    innerGenerator = (IEmbeddingGenerator<string, Embedding<float>>)existingDescriptor.ImplementationInstance;
                }
                else
                {
                    var implementationType = existingDescriptor.ImplementationType ?? typeof(IEmbeddingGenerator<string, Embedding<float>>);
                    innerGenerator = (IEmbeddingGenerator<string, Embedding<float>>)ActivatorUtilities.GetServiceOrCreateInstance(provider, implementationType)!;
                }

                var opts = provider.GetRequiredService<LocalEmbeddingsOpenTelemetryOptions>();
                var logger = provider.GetService<Microsoft.Extensions.Logging.ILogger<InstrumentedEmbeddingGenerator>>();
                
                return new InstrumentedEmbeddingGenerator(innerGenerator, opts, logger);
            },
            existingDescriptor.Lifetime));

        return services;
    }
}

