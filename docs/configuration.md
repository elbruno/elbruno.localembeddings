# Configuration — ElBruno.LocalEmbeddings

## Options

Configure the embedding generator using `LocalEmbeddingsOptions`:

```csharp
var options = new LocalEmbeddingsOptions
{
    ModelName = "sentence-transformers/all-MiniLM-L6-v2",
    CacheDirectory = @"C:\models\cache",
    MaxSequenceLength = 256,
    EnsureModelDownloaded = true,
    NormalizeEmbeddings = false
};
```

## Supported Models

### Default Model

The default model is **sentence-transformers/all-MiniLM-L6-v2**, which produces 384-dimensional embeddings.

### Custom ONNX Models

Any HuggingFace sentence transformer model with ONNX exports is supported:

```csharp
var options = new LocalEmbeddingsOptions
{
    ModelName = "sentence-transformers/paraphrase-MiniLM-L6-v2"
};
```

### Local Model Path

For offline scenarios, specify a local directory containing `model.onnx` and tokenizer files:

```csharp
var options = new LocalEmbeddingsOptions
{
    ModelPath = @"C:\models\my-custom-model",
    EnsureModelDownloaded = false
};
```

## Cache Locations

| Platform | Cache Directory |
|----------|-----------------|
| **Windows** | `%LOCALAPPDATA%\LocalEmbeddings\models` |
| **Linux** | `$XDG_DATA_HOME/LocalEmbeddings/models` or `~/.local/share/LocalEmbeddings/models` |
| **macOS** | `~/.local/share/LocalEmbeddings/models` |

Override with `CacheDirectory`:

```csharp
var options = new LocalEmbeddingsOptions
{
    CacheDirectory = "/custom/cache/path"
};
```
