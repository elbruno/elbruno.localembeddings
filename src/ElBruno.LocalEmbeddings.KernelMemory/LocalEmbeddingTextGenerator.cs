using elbruno.LocalEmbeddings.Options;
using Microsoft.Extensions.AI;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;

namespace elbruno.LocalEmbeddings.KernelMemory;

/// <summary>
/// Adapts an <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> (Microsoft.Extensions.AI)
/// to Kernel Memory's <see cref="ITextEmbeddingGenerator"/> interface.
/// </summary>
/// <remarks>
/// <para>
/// This adapter bridges the gap between the M.E.AI embedding abstraction used by
/// <c>elbruno.LocalEmbeddings</c> and the Kernel Memory embedding interface, enabling
/// local ONNX-based embeddings to be used directly with Kernel Memory pipelines.
/// </para>
/// <para>
/// The adapter implements <see cref="ITextTokenizer"/> (required by <see cref="ITextEmbeddingGenerator"/>)
/// using a simple word-boundary heuristic for token counting. For more accurate token counts,
/// supply a custom <see cref="ITextTokenizer"/> via the constructor overload.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> This class is thread-safe if the underlying
/// <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> is thread-safe (which
/// <see cref="LocalEmbeddingGenerator"/> is after construction).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using var generator = new LocalEmbeddingGenerator(new LocalEmbeddingsOptions());
/// var adapter = new LocalEmbeddingTextGenerator(generator);
/// 
/// var memory = new KernelMemoryBuilder()
///     .WithCustomEmbeddingGenerator(adapter)
///     .Build();
/// </code>
/// </example>
public sealed class LocalEmbeddingTextGenerator : ITextEmbeddingGenerator, IDisposable
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;
    private readonly ITextTokenizer? _customTokenizer;
    private readonly bool _ownsGenerator;

    /// <summary>
    /// Gets the maximum number of tokens the embedding model can process.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="LocalEmbeddingsOptions.MaxSequenceLength"/> (512).
    /// Override via the constructor parameter.
    /// </remarks>
    public int MaxTokens { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalEmbeddingTextGenerator"/> class.
    /// </summary>
    /// <param name="generator">The M.E.AI embedding generator to wrap.</param>
    /// <param name="maxTokens">
    /// Maximum tokens the model supports. Defaults to 512 (matching the default
    /// <see cref="LocalEmbeddingsOptions.MaxSequenceLength"/>).
    /// </param>
    /// <param name="customTokenizer">
    /// Optional custom tokenizer for accurate token counting. When <c>null</c>,
    /// a simple word-boundary heuristic is used.
    /// </param>
    /// <param name="ownsGenerator">
    /// Whether this adapter owns and should dispose the <paramref name="generator"/>
    /// when <see cref="Dispose"/> is called. Defaults to <c>false</c>.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="generator"/> is null.</exception>
    public LocalEmbeddingTextGenerator(
        IEmbeddingGenerator<string, Embedding<float>> generator,
        int maxTokens = 512,
        ITextTokenizer? customTokenizer = null,
        bool ownsGenerator = false)
    {
        ArgumentNullException.ThrowIfNull(generator);

        _generator = generator;
        _customTokenizer = customTokenizer;
        _ownsGenerator = ownsGenerator;
        MaxTokens = maxTokens;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Generates an embedding for a single text input by delegating to the underlying
    /// <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> and converting the result
    /// to Kernel Memory's <see cref="Microsoft.KernelMemory.Embedding"/> struct.
    /// </remarks>
    public async Task<Microsoft.KernelMemory.Embedding> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        var result = await _generator.GenerateAsync(
            [text],
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var vector = result[0].Vector;
        return new Microsoft.KernelMemory.Embedding(vector);
    }

    /// <inheritdoc />
    /// <remarks>
    /// When a custom <see cref="ITextTokenizer"/> is provided, delegates to it.
    /// Otherwise uses a simple heuristic: splits on whitespace and counts the resulting segments.
    /// This approximation is generally adequate for chunking decisions in Kernel Memory.
    /// </remarks>
    public int CountTokens(string text)
    {
        if (_customTokenizer is not null)
        {
            return _customTokenizer.CountTokens(text);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        // Simple word-boundary heuristic — one token ≈ one whitespace-delimited word
        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    /// <inheritdoc />
    /// <remarks>
    /// When a custom <see cref="ITextTokenizer"/> is provided, delegates to it.
    /// Otherwise returns whitespace-delimited words as an approximation of tokens.
    /// </remarks>
    public IReadOnlyList<string> GetTokens(string text)
    {
        if (_customTokenizer is not null)
        {
            return _customTokenizer.GetTokens(text);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// Disposes the adapter and optionally the underlying generator.
    /// </summary>
    /// <remarks>
    /// The underlying <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> is only disposed
    /// if <c>ownsGenerator</c> was set to <c>true</c> in the constructor.
    /// </remarks>
    public void Dispose()
    {
        if (_ownsGenerator && _generator is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
