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

- Model card: [sentence-transformers/all-MiniLM-L6-v2](https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2)

### Custom ONNX Models

Any HuggingFace sentence transformer model with ONNX exports is supported:

```csharp
var options = new LocalEmbeddingsOptions
{
    ModelName = "sentence-transformers/paraphrase-MiniLM-L6-v2"
};
```

For non-default model walkthroughs (including local download + license notes), see [Alternative Models](alternative-models.md).

Google EmbeddingGemma 2 uses a dedicated companion package rather than the core `ModelName` option, because its SentencePiece tokenizer and ONNX graph have a different inference contract. See the [EmbeddingGemma integration guide](embeddinggemma-integration.md) for installation, task prompts, output dimensions, and local/offline configuration.

Common alternatives with model cards:

Estimated download sizes are approximate and can vary by ONNX variant and tokenizer assets.

- [`sentence-transformers/all-MiniLM-L12-v2` (~130–140 MB)](https://huggingface.co/sentence-transformers/all-MiniLM-L12-v2)
- [`sentence-transformers/paraphrase-MiniLM-L6-v2` (~90–100 MB)](https://huggingface.co/sentence-transformers/paraphrase-MiniLM-L6-v2)
- [`BAAI/bge-large-en-v1.5` (large, ~1.3 GB)](https://huggingface.co/BAAI/bge-large-en-v1.5)
- [`intfloat/e5-large-v2` (large, ~1.3 GB)](https://huggingface.co/intfloat/e5-large-v2)

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

Need guidance on choosing models for your use case? See [Alternative Models](alternative-models.md) or check out detailed comparisons on [ElBruno.com](https://elbruno.com).

## Cache Locations

- **Windows**: `%LOCALAPPDATA%\LocalEmbeddings\models`
- **Linux**: `$XDG_DATA_HOME/LocalEmbeddings/models` or `~/.local/share/LocalEmbeddings/models`
- **macOS**: `~/.local/share/LocalEmbeddings/models`

Override with `CacheDirectory`:

```csharp
var options = new LocalEmbeddingsOptions
{
    CacheDirectory = "/custom/cache/path"
};
```

Downloads sharing a model directory are serialized across processes using a
`.download.lock` file. Waiting honors cancellation and fails with a
`TimeoutException` after 30 minutes. If the lock file cannot be created or opened,
the operation fails rather than downloading without synchronization. The image
downloader wraps these errors in its usual `InvalidOperationException`.
Complete, verified cached models are read without rewriting their hash sidecars
or acquiring the download lock.

Cross-process coordination requires working file locks. Do not disable .NET file
locking when sharing a cache. On NFS, use a writable lock file and verify that the
file system supports exclusive locks; a read-only lock handle may not exclude
other clients.
