# Alternative Embeddings Models (Non-Default)

This guide shows two scenarios using free-to-use models other than the default `sentence-transformers/all-MiniLM-L6-v2`.

## Scenario 1: Download a Model Locally and Use `ModelPath`

This is useful for offline or controlled environments where you want to manage model files yourself.

### 1) Pick a free model

- Model used in this scenario: `sentence-transformers/paraphrase-MiniLM-L6-v2`
- License: Apache-2.0
- Model card: https://huggingface.co/sentence-transformers/paraphrase-MiniLM-L6-v2

### 2) Download model files to a local folder

The library expects `model.onnx` and tokenizer assets in the same folder.

```bash
mkdir -p ./models/paraphrase-MiniLM-L6-v2
cd ./models/paraphrase-MiniLM-L6-v2

curl -L -o model.onnx https://huggingface.co/sentence-transformers/paraphrase-MiniLM-L6-v2/resolve/main/onnx/model.onnx
curl -L -O https://huggingface.co/sentence-transformers/paraphrase-MiniLM-L6-v2/resolve/main/tokenizer.json
curl -L -O https://huggingface.co/sentence-transformers/paraphrase-MiniLM-L6-v2/resolve/main/tokenizer_config.json
curl -L -O https://huggingface.co/sentence-transformers/paraphrase-MiniLM-L6-v2/resolve/main/vocab.txt
```

### 3) Configure LocalEmbeddings to use local files

```csharp
using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Options;

var options = new LocalEmbeddingsOptions
{
    ModelPath = "./models/paraphrase-MiniLM-L6-v2",
    EnsureModelDownloaded = false
};

using var generator = new LocalEmbeddingGenerator(options);
var embedding = await generator.GenerateAsync(["Local model path example"]);
Console.WriteLine($"Dimensions: {embedding[0].Vector.Length}");
```

## Scenario 2: Hello World with Another Free Model

Use a different non-default model directly from Hugging Face through `ModelName`.

- Model used in this scenario: `sentence-transformers/all-MiniLM-L12-v2` (Apache-2.0)
- Sample project: [`samples/HelloWorldAltModel`](../samples/HelloWorldAltModel)

```bash
dotnet run --project samples/HelloWorldAltModel
```

The app creates `LocalEmbeddingGenerator` with the model above, generates one embedding, and prints model name + embedding dimensions.

## License Notes for Models Used Here

| Model | License | Source |
|-------|---------|--------|
| `sentence-transformers/paraphrase-MiniLM-L6-v2` | Apache-2.0 | https://huggingface.co/sentence-transformers/paraphrase-MiniLM-L6-v2 |
| `sentence-transformers/all-MiniLM-L12-v2` | Apache-2.0 | https://huggingface.co/sentence-transformers/all-MiniLM-L12-v2 |

Always verify the model card license before shipping to production, because model licenses can change independently from this library.
