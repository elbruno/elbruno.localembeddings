namespace ElBruno.LocalEmbeddings.Npu.Intel.Options;

/// <summary>
/// Configuration options for <see cref="IntelEmbeddingGenerator"/>.
/// </summary>
/// <remarks>
/// <para>
/// These options configure embedding generation using the Intel OpenVINO execution provider,
/// which targets the Intel AI Boost NPU found in Intel Core Ultra processors.
/// </para>
/// <para>
/// The OpenVINO EP supports FP32, FP16, and INT8 models. Quantized INT8 models are
/// recommended for best NPU throughput.
/// </para>
/// <para>
/// <strong>Prerequisites:</strong> Intel OpenVINO runtime must be installed and environment
/// variables set (run <c>setupvars.bat</c> before launching your application).
/// </para>
/// </remarks>
public sealed class IntelEmbeddingsOptions
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
    /// Default is true for Intel NPU, as quantized models run more efficiently.
    /// </summary>
    public bool PreferQuantized { get; set; } = true;

    /// <summary>
    /// Gets or sets the OpenVINO device type to use for inference.
    /// Default is "NPU" which targets Intel AI Boost.
    /// </summary>
    /// <remarks>
    /// <para>Available device types:</para>
    /// <list type="bullet">
    /// <item><description><c>NPU</c> — Intel AI Boost neural processing unit (recommended)</description></item>
    /// <item><description><c>CPU</c> — Intel CPU with OpenVINO optimizations</description></item>
    /// <item><description><c>GPU</c> — Intel integrated/discrete GPU</description></item>
    /// <item><description><c>AUTO</c> — Automatic device selection</description></item>
    /// </list>
    /// </remarks>
    public string DeviceType { get; set; } = "NPU";

    /// <summary>
    /// Gets or sets whether to fall back to CPU execution if OpenVINO NPU is not available.
    /// Default is true.
    /// </summary>
    public bool FallbackToCpu { get; set; } = true;

    /// <summary>
    /// Gets or sets the expected SHA-256 hash (lowercase hex string) of the primary ONNX model file.
    /// When set, the downloaded model file is verified against this hash after download.
    /// </summary>
    public string? ExpectedHash { get; set; }
}
