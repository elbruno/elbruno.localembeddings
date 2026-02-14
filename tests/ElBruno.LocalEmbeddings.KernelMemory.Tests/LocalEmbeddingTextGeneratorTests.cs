using ElBruno.LocalEmbeddings.KernelMemory;
using Microsoft.Extensions.AI;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;

namespace ElBruno.LocalEmbeddings.KernelMemory.Tests;

public class LocalEmbeddingTextGeneratorTests
{
    [Fact]
    public void Constructor_WithNullGenerator_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new LocalEmbeddingTextGenerator(null!));
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_UsesWrappedGenerator()
    {
        var generator = new TestEmbeddingGenerator(new float[] { 0.25f, 0.5f, 0.75f });
        var adapter = new LocalEmbeddingTextGenerator(generator);

        _ = await adapter.GenerateEmbeddingAsync("hello");

        Assert.Equal(1, generator.GenerateCallCount);
        Assert.Equal("hello", generator.LastInput);
    }

    [Theory]
    [InlineData("hello world", 2)]
    [InlineData("  one   two   three  ", 3)]
    [InlineData("", 0)]
    [InlineData("   ", 0)]
    public void CountTokens_WithoutCustomTokenizer_UsesWhitespaceHeuristic(string text, int expected)
    {
        var generator = new TestEmbeddingGenerator();
        var adapter = new LocalEmbeddingTextGenerator(generator);

        var count = adapter.CountTokens(text);

        Assert.Equal(expected, count);
    }

    [Fact]
    public void CountTokens_WithCustomTokenizer_UsesTokenizer()
    {
        var generator = new TestEmbeddingGenerator();
        var tokenizer = new CustomTokenizer();
        var adapter = new LocalEmbeddingTextGenerator(generator, customTokenizer: tokenizer);

        var count = adapter.CountTokens("abc");

        Assert.Equal(42, count);
    }

    [Fact]
    public void GetTokens_WithoutCustomTokenizer_UsesWhitespaceSplit()
    {
        var generator = new TestEmbeddingGenerator();
        var adapter = new LocalEmbeddingTextGenerator(generator);

        var tokens = adapter.GetTokens("one   two\tthree");

        Assert.Equal(["one", "two", "three"], tokens);
    }

    [Fact]
    public void GetTokens_WithCustomTokenizer_UsesTokenizer()
    {
        var generator = new TestEmbeddingGenerator();
        var tokenizer = new CustomTokenizer();
        var adapter = new LocalEmbeddingTextGenerator(generator, customTokenizer: tokenizer);

        var tokens = adapter.GetTokens("ignored");

        Assert.Equal(["custom", "tokens"], tokens);
    }

    [Fact]
    public void Dispose_WhenOwnsGeneratorFalse_DoesNotDisposeWrappedGenerator()
    {
        var generator = new TestEmbeddingGenerator();
        var adapter = new LocalEmbeddingTextGenerator(generator, ownsGenerator: false);

        adapter.Dispose();

        Assert.False(generator.DisposeCalled);
    }

    [Fact]
    public void Dispose_WhenOwnsGeneratorTrue_DisposesWrappedGenerator()
    {
        var generator = new TestEmbeddingGenerator();
        var adapter = new LocalEmbeddingTextGenerator(generator, ownsGenerator: true);

        adapter.Dispose();

        Assert.True(generator.DisposeCalled);
    }

    [Fact]
    public async Task DisposeAsync_WhenOwnsGeneratorTrue_UsesAsyncDisposal()
    {
        var generator = new TestEmbeddingGenerator();
        var adapter = new LocalEmbeddingTextGenerator(generator, ownsGenerator: true);

        await adapter.DisposeAsync();

        Assert.True(generator.DisposeAsyncCalled);
    }

    private sealed class CustomTokenizer : ITextTokenizer
    {
        public int CountTokens(string text) => 42;

        public IReadOnlyList<string> GetTokens(string text) => ["custom", "tokens"];
    }

    private sealed class TestEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>, IDisposable, IAsyncDisposable
    {
        private readonly float[] _vector;

        public TestEmbeddingGenerator(float[]? vector = null)
        {
            _vector = vector ?? [0.1f, 0.2f, 0.3f];
            Metadata = new EmbeddingGeneratorMetadata(
                providerName: "test",
                providerUri: new Uri("https://example.com"),
                defaultModelId: "test-model",
                defaultModelDimensions: _vector.Length);
        }

        public bool DisposeCalled { get; private set; }

        public bool DisposeAsyncCalled { get; private set; }

        public int GenerateCallCount { get; private set; }

        public string? LastInput { get; private set; }

        public EmbeddingGeneratorMetadata Metadata { get; }

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GenerateCallCount++;
            LastInput = values.FirstOrDefault();
            var output = new GeneratedEmbeddings<Embedding<float>>([new Embedding<float>(_vector)]);
            return Task.FromResult(output);
        }

        public TService? GetService<TService>(object? key = null) where TService : class
        {
            if (typeof(TService) == typeof(IEmbeddingGenerator<string, Embedding<float>>))
            {
                return (TService)(object)this;
            }

            if (typeof(TService) == typeof(EmbeddingGeneratorMetadata))
            {
                return (TService)(object)Metadata;
            }

            return null;
        }

        public object? GetService(Type serviceType, object? key = null)
        {
            if (serviceType == typeof(IEmbeddingGenerator<string, Embedding<float>>))
            {
                return this;
            }

            return serviceType == typeof(EmbeddingGeneratorMetadata)
                ? Metadata
                : null;
        }

        public void Dispose() => DisposeCalled = true;

        public ValueTask DisposeAsync()
        {
            DisposeAsyncCalled = true;
            return ValueTask.CompletedTask;
        }
    }
}
