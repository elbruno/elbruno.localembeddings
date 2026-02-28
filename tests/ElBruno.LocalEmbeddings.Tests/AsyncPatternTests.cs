using System.Reflection;
using ElBruno.LocalEmbeddings.Extensions;
using ElBruno.LocalEmbeddings.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace ElBruno.LocalEmbeddings.Tests;

/// <summary>
/// Tests for SEC-007 / PERF-04: async factory pattern on <see cref="LocalEmbeddingGenerator"/>
/// and DI registration in <see cref="ServiceCollectionExtensions"/>.
/// </summary>
public class AsyncPatternTests
{
    // -------------------------------------------------------------------------
    // SEC-007: CreateAsync static factory method exists
    // -------------------------------------------------------------------------

    [Fact]
    public void LocalEmbeddingGenerator_CreateAsync_MethodExists()
    {
        // Verify via reflection that at least one public static CreateAsync method exists.
        // Multiple overloads are expected (with/without options, with/without progress).
        var methods = typeof(LocalEmbeddingGenerator)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == "CreateAsync")
            .ToArray();

        Assert.NotEmpty(methods);

        // Every overload must return Task<LocalEmbeddingGenerator>.
        foreach (var method in methods)
        {
            Assert.Equal(typeof(Task<LocalEmbeddingGenerator>), method.ReturnType);
        }
    }

    // -------------------------------------------------------------------------
    // PERF-04: DI registration compiles and registers IEmbeddingGenerator
    // -------------------------------------------------------------------------

    [Fact]
    public void ServiceCollectionExtensions_AddLocalEmbeddings_RegistersService()
    {
        var services = new ServiceCollection();

        services.AddLocalEmbeddings(options =>
        {
            options.ModelName = "test/model";
            options.EnsureModelDownloaded = false;
            options.ModelPath = Path.GetTempPath();
        });

        // The generator must be registered as IEmbeddingGenerator<string, Embedding<float>>.
        Assert.Contains(
            services,
            s => s.ServiceType == typeof(IEmbeddingGenerator<string, Embedding<float>>));
    }
}
