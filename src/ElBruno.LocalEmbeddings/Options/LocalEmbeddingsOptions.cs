namespace ElBruno.LocalEmbeddings.Options;

/// <summary>
/// Configuration options for <see cref="LocalEmbeddingGenerator"/>.
/// </summary>
public sealed class LocalEmbeddingsOptions
{
    /// <summary>
    /// Gets or sets the HuggingFace model name to use.
    /// Default is "sentence-transformers/all-MiniLM-L6-v2".
    /// </summary>
    public string ModelName { get; set; } = "sentence-transformers/all-MiniLM-L6-v2";

    /// <summary>
    /// Gets or sets the path to a local model directory.
    /// If specified, the model will be loaded from this path instead of being downloaded.
    /// </summary>
    public string? ModelPath { get; set; }

    /// <summary>
    /// Gets or sets the directory where models are cached.
    /// If null, uses the default cache directory.
    /// </summary>
    public string? CacheDirectory { get; set; }

    /// <summary>
    /// Gets or sets the maximum sequence length for tokenization.
    /// Default is 512.
    /// </summary>
    public int MaxSequenceLength { get; set; } = 512;

    /// <summary>
    /// Gets or sets whether to ensure the model is downloaded on startup.
    /// Default is true. Set to false if <see cref="ModelPath"/> is specified.
    /// </summary>
    public bool EnsureModelDownloaded { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to L2-normalize embedding vectors to unit length.
    /// Default is false.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When enabled, embeddings are normalized to have a magnitude of 1 (unit vectors).
    /// This matches the default behavior of sentence-transformers in Python.
    /// </para>
    /// <para>
    /// Normalized embeddings have the property that cosine similarity equals the dot product,
    /// which can simplify and accelerate similarity computations.
    /// </para>
    /// </remarks>
    public bool NormalizeEmbeddings { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether ONNX Runtime should use parallel execution mode.
    /// Default is true.
    /// </summary>
    /// <remarks>
    /// Set this to <see langword="false"/> on low-resource devices to reduce CPU pressure.
    /// </remarks>
    public bool UseParallelExecution { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to prefer a quantized model variant (INT8) when available.
    /// Default is false.
    /// </summary>
    /// <remarks>
    /// When enabled, the downloader and model loader will prefer <c>model_quantized.onnx</c>
    /// or <c>model_int8.onnx</c>, and fall back to <c>model.onnx</c>.
    /// </remarks>
    public bool PreferQuantized { get; set; } = false;

    /// <summary>
    /// Gets or sets the number of inter-op threads used by ONNX Runtime.
    /// If null, defaults to <see cref="Environment.ProcessorCount"/>.
    /// </summary>
    public int? InterOpNumThreads { get; set; }

    /// <summary>
    /// Gets or sets the number of intra-op threads used by ONNX Runtime.
    /// If null, defaults to <see cref="Environment.ProcessorCount"/>.
    /// </summary>
    public int? IntraOpNumThreads { get; set; }

    /// <summary>
    /// Gets or sets the expected SHA-256 hash (lowercase hex string) of the primary ONNX model file.
    /// When set, the downloaded model file is verified against this hash after download.
    /// </summary>
    /// <remarks>
    /// If the hash does not match, an <see cref="InvalidOperationException"/> is thrown.
    /// Leave <see langword="null"/> to skip hash verification (default behavior).
    /// </remarks>
    public string? ExpectedHash { get; set; }

    /// <summary>
    /// Gets or sets the batch size mode for embedding generation.
    /// Default is <see cref="BatchSizeMode.Fixed"/>.
    /// </summary>
    /// <remarks>
    /// When set to <see cref="BatchSizeMode.Auto"/>, the library profiles inference
    /// during the first batch and automatically selects the optimal batch size
    /// based on throughput and memory characteristics.
    /// </remarks>
    public BatchSizeMode BatchSizeMode { get; set; } = BatchSizeMode.Fixed;

    /// <summary>
    /// Gets or sets the fixed batch size when <see cref="BatchSizeMode"/> is <see cref="BatchSizeMode.Fixed"/>.
    /// Default is 32.
    /// </summary>
    /// <remarks>
    /// This value is ignored when <see cref="BatchSizeMode"/> is <see cref="BatchSizeMode.Auto"/>.
    /// </remarks>
    public int BatchSize { get; set; } = 32;

    /// <summary>
    /// Gets or sets the minimum batch size for auto-tuning.
    /// Default is 4.
    /// </summary>
    /// <remarks>
    /// Only used when <see cref="BatchSizeMode"/> is <see cref="BatchSizeMode.Auto"/>.
    /// The auto-tuner will not select a batch size smaller than this value.
    /// </remarks>
    public int MinBatchSize { get; set; } = 4;

    /// <summary>
    /// Gets or sets the maximum batch size for auto-tuning.
    /// Default is 128.
    /// </summary>
    /// <remarks>
    /// Only used when <see cref="BatchSizeMode"/> is <see cref="BatchSizeMode.Auto"/>.
    /// The auto-tuner will not select a batch size larger than this value.
    /// </remarks>
    public int MaxBatchSize { get; set; } = 128;
}
