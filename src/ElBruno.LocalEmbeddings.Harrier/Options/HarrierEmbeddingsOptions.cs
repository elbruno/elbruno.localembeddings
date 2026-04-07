namespace ElBruno.LocalEmbeddings.Harrier.Options;

/// <summary>
/// Configuration options for Harrier embedding generation.
/// </summary>
public sealed class HarrierEmbeddingsOptions
{
    /// <summary>
    /// Default HuggingFace model name for Harrier-OSS-v1 270M (ONNX).
    /// </summary>
    public const string DefaultModelName = "onnx-community/harrier-oss-v1-270m-ONNX";

    /// <summary>
    /// Default instruction prefix for retrieval tasks.
    /// </summary>
    public const string DefaultInstructionPrefix = "Instruct: Retrieve semantically similar text\nQuery: ";

    /// <summary>
    /// Gets or sets the HuggingFace model name to use.
    /// Default is <see cref="DefaultModelName"/>.
    /// </summary>
    public string ModelName { get; set; } = DefaultModelName;

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
    /// Default is 8192. The model supports up to 32,768 tokens, but 8192 is a practical default
    /// for most use cases while managing memory and latency.
    /// </summary>
    public int MaxSequenceLength { get; set; } = 8192;

    /// <summary>
    /// Gets or sets whether to ensure the model is downloaded on startup.
    /// Default is true. Set to false if <see cref="ModelPath"/> is specified.
    /// </summary>
    public bool EnsureModelDownloaded { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether ONNX Runtime should use parallel execution mode.
    /// Default is true.
    /// </summary>
    public bool UseParallelExecution { get; set; } = true;

    /// <summary>
    /// Gets or sets the ONNX model variant to use.
    /// Default is <see cref="HarrierModelVariant.Quantized"/>.
    /// </summary>
    public HarrierModelVariant ModelVariant { get; set; } = HarrierModelVariant.Quantized;

    /// <summary>
    /// Gets or sets the instruction prefix prepended to input text before tokenization.
    /// Default is <see cref="DefaultInstructionPrefix"/>.
    /// Set to <see langword="null"/> or empty to disable instruction prefixing.
    /// </summary>
    /// <remarks>
    /// Harrier is an instruction-tuned model. Using an appropriate instruction prefix
    /// significantly improves embedding quality for the target task. Common prefixes include:
    /// <list type="bullet">
    ///   <item>Retrieval: <c>"Instruct: Retrieve semantically similar text\nQuery: "</c></item>
    ///   <item>Classification: <c>"Instruct: Classify the following text\nQuery: "</c></item>
    ///   <item>Clustering: <c>"Instruct: Identify the topic or theme of the following text\nQuery: "</c></item>
    /// </list>
    /// </remarks>
    public string? InstructionPrefix { get; set; } = DefaultInstructionPrefix;

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
    /// Gets or sets a value indicating whether to use DirectML GPU acceleration (Windows only).
    /// Default is false (CPU inference). When true on Windows, uses DirectML execution provider.
    /// </summary>
    /// <remarks>
    /// DirectML acceleration requires a DirectX 12 compatible GPU (most discrete and integrated GPUs
    /// on Windows 10/11 qualify). Falls back gracefully to CPU if DirectML is unavailable.
    /// On non-Windows platforms this property has no effect.
    /// </remarks>
    public bool UseDirectML { get; set; } = false;

    /// <summary>
    /// Gets or sets the DirectML device ID to use for GPU acceleration.
    /// Default is 0 (first GPU). Only used when <see cref="UseDirectML"/> is true.
    /// </summary>
    public int DirectMLDeviceId { get; set; } = 0;

    /// <summary>
    /// Gets or sets the expected SHA-256 hash (lowercase hex string) of the primary ONNX model file.
    /// When set, the downloaded model file is verified against this hash after download.
    /// </summary>
    public string? ExpectedHash { get; set; }
}
