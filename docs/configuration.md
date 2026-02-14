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
    NormalizeEmbeddings = false,
    PreferQuantized = false
};
```

For async applications (ASP.NET, UI), prefer:

```csharp
await using var generator = await LocalEmbeddingGenerator.CreateAsync(options);
```

The synchronous constructor remains available for compatibility but can block during model resolution/download.

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

For non-default model walkthroughs (including local download + license notes), see [Alternative Models](alternative-models.md).

### Quantized ONNX Model Preference

You can prefer INT8 quantized model variants when available:

```csharp
var options = new LocalEmbeddingsOptions
{
    ModelName = "sentence-transformers/all-MiniLM-L6-v2",
    PreferQuantized = true
};
```

When enabled, LocalEmbeddings tries these files in order:

1. `onnx/model_quantized.onnx`
2. `onnx/model_int8.onnx`
3. `onnx/model.onnx` (fallback)

Quantized models are typically much smaller and can reduce memory usage, with a small potential quality trade-off depending on the model.

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
