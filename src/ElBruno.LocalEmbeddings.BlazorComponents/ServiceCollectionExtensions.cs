using Microsoft.Extensions.DependencyInjection;

namespace ElBruno.LocalEmbeddings.BlazorComponents;

/// <summary>Extension methods for registering BlazorComponents services.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="EmbeddingStateService"/> (scoped) so all BlazorComponents work
    /// correctly within a Blazor Server or WebAssembly application.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddLocalEmbeddingsBlazorComponents(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<EmbeddingStateService>();
        return services;
    }
}
