using ElBruno.LocalEmbeddings.BlazorComponents;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ElBruno.LocalEmbeddings.BlazorComponents.Tests;

/// <summary>
/// Tests for <see cref="ServiceCollectionExtensions"/>.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddLocalEmbeddingsBlazorComponents_RegistersEmbeddingStateService()
    {
        var services = new ServiceCollection();

        services.AddLocalEmbeddingsBlazorComponents();

        var provider = services.BuildServiceProvider();
        var svc = provider.GetService<EmbeddingStateService>();
        Assert.NotNull(svc);
    }

    [Fact]
    public void AddLocalEmbeddingsBlazorComponents_EmbeddingStateServiceIsScoped()
    {
        var services = new ServiceCollection();
        services.AddLocalEmbeddingsBlazorComponents();

        var descriptor = services.Single(d => d.ServiceType == typeof(EmbeddingStateService));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddLocalEmbeddingsBlazorComponents_DifferentScopes_ReturnDifferentInstances()
    {
        var services = new ServiceCollection();
        services.AddLocalEmbeddingsBlazorComponents();
        var provider = services.BuildServiceProvider();

        EmbeddingStateService svc1;
        EmbeddingStateService svc2;

        using (var scope1 = provider.CreateScope())
            svc1 = scope1.ServiceProvider.GetRequiredService<EmbeddingStateService>();

        using (var scope2 = provider.CreateScope())
            svc2 = scope2.ServiceProvider.GetRequiredService<EmbeddingStateService>();

        Assert.NotSame(svc1, svc2);
    }

    [Fact]
    public void AddLocalEmbeddingsBlazorComponents_SameScope_ReturnsSameInstance()
    {
        var services = new ServiceCollection();
        services.AddLocalEmbeddingsBlazorComponents();
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var svc1 = scope.ServiceProvider.GetRequiredService<EmbeddingStateService>();
        var svc2 = scope.ServiceProvider.GetRequiredService<EmbeddingStateService>();

        Assert.Same(svc1, svc2);
    }

    [Fact]
    public void AddLocalEmbeddingsBlazorComponents_ReturnsServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddLocalEmbeddingsBlazorComponents();

        Assert.Same(services, result);
    }

    [Fact]
    public void AddLocalEmbeddingsBlazorComponents_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection? services = null;
        Assert.Throws<ArgumentNullException>(
            () => services!.AddLocalEmbeddingsBlazorComponents());
    }
}
