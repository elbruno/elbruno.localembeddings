namespace ElBruno.LocalEmbeddings.Npu.Qualcomm.Options;

/// <summary>
/// Configuration options for <see cref="QualcommEmbeddingGenerator"/>.
/// </summary>
/// <remarks>
/// <para>
/// These options configure embedding generation using the Qualcomm QNN (Qualcomm Neural Network)
/// execution provider, which targets the Hexagon Tensor Processor (HTP) NPU found in
/// Qualcomm Snapdragon X series processors.
/// </para>
/// <para>
/// INT8 quantized models are strongly recommended for optimal NPU performance on QNN HTP.
/// <see cref="PreferQuantized"/> defaults to <c>true</c>.
/// </para>
/// </remarks>
public sealed class QualcommEmbeddingsOptions
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
    public bool NormalizeEmbeddings { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to prefer a quantized model variant (INT8) when available.
    /// Default is true for Qualcomm NPU, as INT8 models run most efficiently on the HTP backend.
    /// </summary>
    public bool PreferQuantized { get; set; } = true;

    /// <summary>
    /// Gets or sets the QNN backend path to use.
    /// Default is "QnnHtp.dll" which targets the Hexagon Tensor Processor (NPU).
    /// </summary>
    /// <remarks>
    /// <para>Available backends:</para>
    /// <list type="bullet">
    /// <item><description><c>QnnHtp.dll</c> — Hexagon Tensor Processor (NPU, recommended)</description></item>
    /// <item><description><c>QnnCpu.dll</c> — QNN CPU backend</description></item>
    /// </list>
    /// </remarks>
    public string QnnBackendPath { get; set; } = "QnnHtp.dll";

    /// <summary>
    /// Gets or sets whether to fall back to CPU execution if QNN is not available.
    /// Default is true.
    /// </summary>
    public bool FallbackToCpu { get; set; } = true;

    /// <summary>
    /// Gets or sets the expected SHA-256 hash (lowercase hex string) of the primary ONNX model file.
    /// When set, the downloaded model file is verified against this hash after download.
    /// </summary>
    public string? ExpectedHash { get; set; }
}
