using ElBruno.LocalEmbeddings.Options;
using Microsoft.Extensions.AI;
using Microsoft.KernelMemory;

namespace ElBruno.LocalEmbeddings.KernelMemory.Extensions;

/// <summary>
/// Extension methods for <see cref="IKernelMemoryBuilder"/> to register local ONNX-based
/// embeddings via <c>ElBruno.LocalEmbeddings</c>.
/// </summary>
public static class KernelMemoryBuilderExtensions
{
    /// <summary>
    /// Registers a <see cref="LocalEmbeddingGenerator"/>-backed embedding generator
    /// with the Kernel Memory builder using default options.
    /// </summary>
    /// <param name="builder">The Kernel Memory builder.</param>
    /// <returns>The builder for chaining.</returns>
    /// <remarks>
    /// <para>
    /// Creates a new <see cref="LocalEmbeddingGenerator"/> with default
    /// <see cref="LocalEmbeddingsOptions"/> and wraps it in a
    /// <see cref="LocalEmbeddingTextGenerator"/> adapter. The adapter takes
    /// ownership of the generator and will dispose it when the memory instance
    /// is disposed.
    /// </para>
    /// <para>
    /// Uses the <c>sentence-transformers/all-MiniLM-L6-v2</c> model by default.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var memory = new KernelMemoryBuilder()
    ///     .WithOllamaTextGeneration(config)
    ///     .WithLocalEmbeddings()
    ///     .Build();
    /// </code>
    /// </example>
    public static IKernelMemoryBuilder WithLocalEmbeddings(this IKernelMemoryBuilder builder)
    {
        return builder.WithLocalEmbeddings(new LocalEmbeddingsOptions());
    }

    /// <summary>
    /// Registers a <see cref="LocalEmbeddingGenerator"/>-backed embedding generator
    /// with the Kernel Memory builder using the specified options.
    /// </summary>
    /// <param name="builder">The Kernel Memory builder.</param>
    /// <param name="options">The options to configure the local embedding generator.</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="builder"/> or <paramref name="options"/> is null.
    /// </exception>
    /// <example>
    /// <code>
    /// var memory = new KernelMemoryBuilder()
    ///     .WithOllamaTextGeneration(config)
    ///     .WithLocalEmbeddings(new LocalEmbeddingsOptions
    ///     {
    ///         ModelName = "sentence-transformers/all-MiniLM-L6-v2",
    ///         NormalizeEmbeddings = true
    ///     })
    ///     .Build();
    /// </code>
    /// </example>
    public static IKernelMemoryBuilder WithLocalEmbeddings(
        this IKernelMemoryBuilder builder,
        LocalEmbeddingsOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);

        var generator = new LocalEmbeddingGenerator(options);
        var adapter = new LocalEmbeddingTextGenerator(
            generator,
            maxTokens: options.MaxSequenceLength,
            ownsGenerator: true);

        return builder.WithCustomEmbeddingGenerator(adapter);
    }

    /// <summary>
    /// Registers a <see cref="LocalEmbeddingGenerator"/>-backed embedding generator
    /// with the Kernel Memory builder using a configuration delegate.
    /// </summary>
    /// <param name="builder">The Kernel Memory builder.</param>
    /// <param name="configure">An action to configure <see cref="LocalEmbeddingsOptions"/>.</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
    /// </exception>
    /// <example>
    /// <code>
    /// var memory = new KernelMemoryBuilder()
    ///     .WithOllamaTextGeneration(config)
    ///     .WithLocalEmbeddings(options =>
    ///     {
    ///         options.NormalizeEmbeddings = true;
    ///         options.MaxSequenceLength = 256;
    ///     })
    ///     .Build();
    /// </code>
    /// </example>
    public static IKernelMemoryBuilder WithLocalEmbeddings(
        this IKernelMemoryBuilder builder,
        Action<LocalEmbeddingsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new LocalEmbeddingsOptions();
        configure(options);

        return builder.WithLocalEmbeddings(options);
    }

    /// <summary>
    /// Wraps an existing <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> in a
    /// <see cref="LocalEmbeddingTextGenerator"/> adapter and registers it with the
    /// Kernel Memory builder.
    /// </summary>
    /// <param name="builder">The Kernel Memory builder.</param>
    /// <param name="generator">The M.E.AI embedding generator to use.</param>
    /// <param name="maxTokens">Maximum tokens the model supports. Defaults to 512.</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="builder"/> or <paramref name="generator"/> is null.
    /// </exception>
    /// <remarks>
    /// Use this overload when you already have an <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/>
    /// instance (e.g., resolved from DI) and want to register it with Kernel Memory.
    /// The adapter does <strong>not</strong> take ownership of the generator — the caller
    /// is responsible for its lifecycle.
    /// </remarks>
    public static IKernelMemoryBuilder WithLocalEmbeddings(
        this IKernelMemoryBuilder builder,
        IEmbeddingGenerator<string, Embedding<float>> generator,
        int maxTokens = 512)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(generator);

        var adapter = new LocalEmbeddingTextGenerator(
            generator,
            maxTokens: maxTokens,
            ownsGenerator: false);

        return builder.WithCustomEmbeddingGenerator(adapter);
    }
}
