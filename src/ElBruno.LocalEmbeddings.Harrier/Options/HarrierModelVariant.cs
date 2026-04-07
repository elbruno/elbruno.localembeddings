namespace ElBruno.LocalEmbeddings.Harrier.Options;

/// <summary>
/// Specifies the ONNX model variant to use for Harrier embedding generation.
/// </summary>
public enum HarrierModelVariant
{
    /// <summary>
    /// Full-precision FP32 model. Highest accuracy, largest file size (~1GB+).
    /// </summary>
    Fp32,

    /// <summary>
    /// Half-precision FP16 model. Good accuracy with ~50% size reduction.
    /// </summary>
    Fp16,

    /// <summary>
    /// INT8 quantized model. Smaller and faster with minimal accuracy loss.
    /// </summary>
    Quantized,

    /// <summary>
    /// 4-bit quantized model. Smallest size and fastest inference, with some accuracy trade-off.
    /// </summary>
    Q4
}
