namespace ElBruno.LocalEmbeddings.BlazorComponents;

/// <summary>Download / ready state of an embedding model.</summary>
public enum EmbeddingModelState
{
    /// <summary>Model files have not been downloaded yet.</summary>
    NotDownloaded,

    /// <summary>Model is currently being downloaded.</summary>
    Downloading,

    /// <summary>Model is downloaded and ready for inference.</summary>
    Downloaded
}

/// <summary>Metadata describing a known embedding model.</summary>
public sealed class EmbeddingModelInfo
{
    /// <summary>Unique identifier / model ID (e.g. "sentence-transformers/all-MiniLM-L6-v2").</summary>
    public required string ModelId { get; init; }

    /// <summary>Human-readable display name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Embedding output dimension.</summary>
    public int Dimensions { get; init; }

    /// <summary>Approximate model size on disk in megabytes.</summary>
    public double SizeMb { get; init; }

    /// <summary>Primary language(s) supported (e.g. "English", "Multilingual").</summary>
    public string Language { get; init; } = "English";

    /// <summary>Short description of the model.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Current download / ready state.</summary>
    public EmbeddingModelState State { get; set; } = EmbeddingModelState.NotDownloaded;

    /// <summary>Download progress 0–100, only meaningful when State is Downloading.</summary>
    public int DownloadProgressPercent { get; set; }
}
