# LocalEmbeddings

[![NuGet](https://img.shields.io/nuget/v/elbruno.LocalEmbeddings.svg)](https://www.nuget.org/packages/elbruno.LocalEmbeddings)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A .NET library for generating text embeddings locally using ONNX Runtime and Microsoft.Extensions.AI abstractions—no external API calls required.

## Features

- **Local Embedding Generation** - Run inference entirely on your machine using ONNX Runtime
- **Microsoft.Extensions.AI Integration** - Implements `IEmbeddingGenerator<string, Embedding<float>>` for seamless ecosystem compatibility
- **HuggingFace Model Support** - Use popular sentence transformer models from HuggingFace Hub
- **Automatic Model Caching** - Models are downloaded once and cached locally for fast subsequent loads
- **Dependency Injection Support** - First-class integration with `IServiceCollection` and the Options pattern
- **Thread-Safe** - Concurrent embedding generation from multiple threads
- **Batched Inference** - Efficient processing of multiple texts in a single call

## Installation

```bash
dotnet add package elbruno.LocalEmbeddings
```

Or via the NuGet Package Manager:

```powershell
Install-Package elbruno.LocalEmbeddings
```

## Quick Start

### Direct Usage

```csharp
using LocalEmbeddings;
using LocalEmbeddings.Options;

// Create the generator with default settings
var generator = new LocalEmbeddingGenerator(new LocalEmbeddingsOptions());

// Generate a single embedding
var result = await generator.GenerateAsync(["Hello, world!"]);
float[] embedding = result[0].Vector.ToArray();

// Generate multiple embeddings (batched)
var texts = new[] { "First document", "Second document", "Third document" };
var embeddings = await generator.GenerateAsync(texts);

// Don't forget to dispose when done
generator.Dispose();
```

### With Custom Model

```csharp
var options = new LocalEmbeddingsOptions
{
    ModelName = "sentence-transformers/all-MiniLM-L6-v2",
    MaxSequenceLength = 256
};

using var generator = new LocalEmbeddingGenerator(options);
var embeddings = await generator.GenerateAsync(["Your text here"]);
```

## Configuration Options

The `LocalEmbeddingsOptions` class provides the following configuration:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ModelName` | `string` | `"sentence-transformers/all-MiniLM-L6-v2"` | HuggingFace model identifier |
| `ModelPath` | `string?` | `null` | Path to a local model directory (bypasses download) |
| `CacheDirectory` | `string?` | `null` | Custom directory for model cache |
| `MaxSequenceLength` | `int` | `512` | Maximum token sequence length |
| `EnsureModelDownloaded` | `bool` | `true` | Download model on startup if not cached |

```csharp
var options = new LocalEmbeddingsOptions
{
    ModelName = "sentence-transformers/all-MiniLM-L6-v2",
    CacheDirectory = @"C:\models\cache",
    MaxSequenceLength = 256,
    EnsureModelDownloaded = true
};
```

## Dependency Injection

LocalEmbeddings provides multiple overloads of `AddLocalEmbeddings()` for flexible DI registration:

### Basic Registration

```csharp
using LocalEmbeddings.Extensions;

services.AddLocalEmbeddings();
```

### With Configuration Action

```csharp
services.AddLocalEmbeddings(options =>
{
    options.ModelName = "sentence-transformers/all-MiniLM-L6-v2";
    options.MaxSequenceLength = 256;
});
```

### With Model Name Only

```csharp
services.AddLocalEmbeddings("sentence-transformers/all-MiniLM-L6-v2");
```

### With Pre-configured Options

```csharp
var options = new LocalEmbeddingsOptions
{
    ModelName = "sentence-transformers/all-MiniLM-L6-v2",
    MaxSequenceLength = 256
};
services.AddLocalEmbeddings(options);
```

### From Configuration (appsettings.json)

```json
{
  "LocalEmbeddings": {
    "ModelName": "sentence-transformers/all-MiniLM-L6-v2",
    "MaxSequenceLength": 256,
    "CacheDirectory": "/path/to/cache"
  }
}
```

```csharp
services.AddLocalEmbeddings(configuration.GetSection("LocalEmbeddings"));
```

### Injecting the Generator

```csharp
public class MyService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddings;

    public MyService(IEmbeddingGenerator<string, Embedding<float>> embeddings)
    {
        _embeddings = embeddings;
    }

    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        var result = await _embeddings.GenerateAsync([text]);
        return result[0].Vector.ToArray();
    }
}
```

## Supported Models

### Default Model

The default model is **sentence-transformers/all-MiniLM-L6-v2**, which produces 384-dimensional embeddings. It offers an excellent balance of quality and performance.

### Custom ONNX Models

You can use any HuggingFace sentence transformer model that has ONNX exports:

```csharp
// Use a different HuggingFace model
var options = new LocalEmbeddingsOptions
{
    ModelName = "sentence-transformers/paraphrase-MiniLM-L6-v2"
};
```

### Local Model Path

For offline scenarios or custom models, specify a local directory:

```csharp
var options = new LocalEmbeddingsOptions
{
    ModelPath = @"C:\models\my-custom-model",
    EnsureModelDownloaded = false
};
```

The model directory must contain:

- `model.onnx` - The ONNX model file
- `tokenizer.json` or `vocab.txt` - Tokenizer files

## Cache Locations

Models are automatically cached in platform-specific locations:

| Platform | Cache Directory |
|----------|-----------------|
| **Windows** | `%LOCALAPPDATA%\LocalEmbeddings\models` |
| **Linux** | `$XDG_DATA_HOME/LocalEmbeddings/models` or `~/.local/share/LocalEmbeddings/models` |
| **macOS** | `~/.local/share/LocalEmbeddings/models` |

Override with the `CacheDirectory` option:

```csharp
var options = new LocalEmbeddingsOptions
{
    CacheDirectory = "/custom/cache/path"
};
```

## API Reference

### LocalEmbeddingGenerator

The main class for generating embeddings. Implements `IEmbeddingGenerator<string, Embedding<float>>`.

```csharp
public sealed class LocalEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    // Constructor
    public LocalEmbeddingGenerator(LocalEmbeddingsOptions options);
    
    // Properties
    public EmbeddingGeneratorMetadata Metadata { get; }
    
    // Methods
    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default);
    
    public TService? GetService<TService>(object? key = null) where TService : class;
    
    public void Dispose();
}
```

### LocalEmbeddingsOptions

Configuration options for the embedding generator.

```csharp
public sealed class LocalEmbeddingsOptions
{
    public string ModelName { get; set; }
    public string? ModelPath { get; set; }
    public string? CacheDirectory { get; set; }
    public int MaxSequenceLength { get; set; }
    public bool EnsureModelDownloaded { get; set; }
}
```

### ServiceCollectionExtensions

Extension methods for DI registration in `LocalEmbeddings.Extensions` namespace.

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLocalEmbeddings(
        this IServiceCollection services,
        Action<LocalEmbeddingsOptions>? configure = null);
    
    public static IServiceCollection AddLocalEmbeddings(
        this IServiceCollection services,
        LocalEmbeddingsOptions options);
    
    public static IServiceCollection AddLocalEmbeddings(
        this IServiceCollection services,
        string modelName);
    
    public static IServiceCollection AddLocalEmbeddings(
        this IServiceCollection services,
        IConfiguration configuration);
}
```

## Building from Source

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) or later
- Git

### Build

```bash
git clone https://github.com/elbruno/elbruno.localembeddings.git
cd elbruno.localembeddings
dotnet build
```

### Run Tests

```bash
dotnet test
```

## Requirements

- .NET 10.0 or later
- ONNX Runtime compatible platform (Windows, Linux, macOS)

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
