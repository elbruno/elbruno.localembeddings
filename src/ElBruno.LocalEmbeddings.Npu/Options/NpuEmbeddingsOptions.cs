namespace ElBruno.LocalEmbeddings.Npu.Options;

/// <summary>
/// Configuration options for <see cref="NpuEmbeddingGenerator"/>.
/// </summary>
public sealed class NpuEmbeddingsOptions
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
    /// Default is true for NPU, as quantized models run more efficiently on NPU hardware.
    /// </summary>
    public bool PreferQuantized { get; set; } = true;

    /// <summary>
    /// Gets or sets the DirectML device ID to use for NPU inference.
    /// Default is 0 (first available DirectML device).
    /// </summary>
    /// <remarks>
    /// <para>
    /// DirectML enumerates hardware devices (NPUs, GPUs) in a platform-specific order.
    /// Use 0 for the default device, which is typically the most capable accelerator.
    /// </para>
    /// <para>
    /// On devices with both an NPU and a discrete GPU, you may need to adjust this
    /// value to target the specific hardware you want.
    /// </para>
    /// </remarks>
    public int DeviceId { get; set; } = 0;

    /// <summary>
    /// Gets or sets the expected SHA-256 hash (lowercase hex string) of the primary ONNX model file.
    /// When set, the downloaded model file is verified against this hash after download.
    /// </summary>
    public string? ExpectedHash { get; set; }
}
