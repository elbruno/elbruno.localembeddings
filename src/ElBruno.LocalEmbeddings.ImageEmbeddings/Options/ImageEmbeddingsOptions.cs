namespace ElBruno.LocalEmbeddings.ImageEmbeddings.Options;

/// <summary>
/// Configuration options for CLIP-based image embedding generation.
/// </summary>
public sealed class ImageEmbeddingsOptions
{
    private string _textModelFileName = "text_model.onnx";
    private string _visionModelFileName = "vision_model.onnx";
    private string _vocabFileName = "vocab.json";
    private string _mergesFileName = "merges.txt";

    /// <summary>
    /// Gets or sets the directory containing the CLIP ONNX model files.
    /// Must contain the text model, vision model, vocabulary, and merge files.
    /// </summary>
    public string ModelDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the filename of the CLIP text encoder ONNX model.
    /// Default is "text_model.onnx".
    /// </summary>
    public string TextModelFileName
    {
        get => _textModelFileName;
        set => _textModelFileName = ValidateFileName(value, nameof(TextModelFileName));
    }

    /// <summary>
    /// Gets or sets the filename of the CLIP vision encoder ONNX model.
    /// Default is "vision_model.onnx".
    /// </summary>
    public string VisionModelFileName
    {
        get => _visionModelFileName;
        set => _visionModelFileName = ValidateFileName(value, nameof(VisionModelFileName));
    }

    /// <summary>
    /// Gets or sets the filename of the CLIP vocabulary file.
    /// Default is "vocab.json".
    /// </summary>
    public string VocabFileName
    {
        get => _vocabFileName;
        set => _vocabFileName = ValidateFileName(value, nameof(VocabFileName));
    }

    /// <summary>
    /// Gets or sets the filename of the CLIP BPE merge rules file.
    /// Default is "merges.txt".
    /// </summary>
    public string MergesFileName
    {
        get => _mergesFileName;
        set => _mergesFileName = ValidateFileName(value, nameof(MergesFileName));
    }

    // Use a fixed set that covers Windows-invalid chars and common shell-dangerous chars.
    // Path.GetInvalidFileNameChars() is OS-specific (Linux omits <, >, |, ?, *), so we
    // define a cross-platform superset to ensure consistent security validation everywhere.
    private static readonly char[] _invalidFileNameChars =
        ['<', '>', ':', '"', '|', '?', '*', '\\', '/', '\0'];

    private static string ValidateFileName(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        if (value.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("File name must not contain path traversal sequences ('..').", paramName);
        if (value.IndexOfAny(_invalidFileNameChars) >= 0)
            throw new ArgumentException("File name contains invalid characters.", paramName);
        return value;
    }

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

    /// <summary>
    /// Gets or sets a value indicating whether to ensure the model files are downloaded.
    /// If true, the library will attempt to download missing model files to <see cref="ModelDirectory"/>.
    /// Default is false.
    /// </summary>
    public bool EnsureModelDownloaded { get; set; } = false;
}
