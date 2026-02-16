namespace ElBruno.LocalEmbeddings.ImageEmbeddings.Options;

/// <summary>
/// Configuration options for CLIP-based image embedding generation.
/// </summary>
public sealed class ImageEmbeddingsOptions
{
    /// <summary>
    /// Gets or sets the directory containing the CLIP ONNX model files.
    /// Must contain the text model, vision model, vocabulary, and merge files.
    /// </summary>
    public string ModelDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the filename of the CLIP text encoder ONNX model.
    /// Default is "text_model.onnx".
    /// </summary>
    public string TextModelFileName { get; set; } = "text_model.onnx";

    /// <summary>
    /// Gets or sets the filename of the CLIP vision encoder ONNX model.
    /// Default is "vision_model.onnx".
    /// </summary>
    public string VisionModelFileName { get; set; } = "vision_model.onnx";

    /// <summary>
    /// Gets or sets the filename of the CLIP vocabulary file.
    /// Default is "vocab.json".
    /// </summary>
    public string VocabFileName { get; set; } = "vocab.json";

    /// <summary>
    /// Gets or sets the filename of the CLIP BPE merge rules file.
    /// Default is "merges.txt".
    /// </summary>
    public string MergesFileName { get; set; } = "merges.txt";

    /// <summary>
    /// Gets the full path to the text model ONNX file.
    /// </summary>
    public string TextModelPath => Path.Combine(ModelDirectory, TextModelFileName);

    /// <summary>
    /// Gets the full path to the vision model ONNX file.
    /// </summary>
    public string VisionModelPath => Path.Combine(ModelDirectory, VisionModelFileName);

    /// <summary>
    /// Gets the full path to the vocabulary file.
    /// </summary>
    public string VocabPath => Path.Combine(ModelDirectory, VocabFileName);

    /// <summary>
    /// Gets the full path to the merge rules file.
    /// </summary>
    public string MergesPath => Path.Combine(ModelDirectory, MergesFileName);
}
