# Azure Hybrid Fallback Integration Quick Start

The **ElBruno.LocalEmbeddings.Azure** package provides seamless hybrid embedding generation that tries local embeddings first, then automatically falls back to Azure OpenAI if needed.

## Installation

```bash
dotnet add package ElBruno.LocalEmbeddings.Azure
```

## Setup

### 1. Register Services

```csharp
using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Azure.Extensions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Register local embeddings first
services.AddLocalEmbeddings();

// Add Azure fallback
services.AddLocalEmbeddingsWithAzureFallback(options =>
{
    options.Endpoint = "https://my-resource.openai.azure.com";
    options.ApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");
    options.DeploymentName = "text-embedding-3-small";
    options.MaxFallbackAttempts = 3;
    options.TimeoutMilliseconds = 30_000;
    options.LogFallbackEvents = true;
});

var provider = services.BuildServiceProvider();
var embeddingGenerator = provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
```

### 2. Use Configuration File

Save your settings in `appsettings.json`:

```json
{
  "AzureEmbeddings": {
    "Endpoint": "https://my-resource.openai.azure.com",
    "ApiKey": "your-api-key",
    "DeploymentName": "text-embedding-3-small",
    "MaxFallbackAttempts": 3,
    "TimeoutMilliseconds": 30000,
    "LogFallbackEvents": true
  }
}
```

Then register with configuration:

```csharp
services.AddLocalEmbeddings();
services.AddLocalEmbeddingsWithAzureFallback("AzureEmbeddings");
```

### 3. Generate Embeddings

```csharp
// Call the generator - it automatically tries local first, then Azure
var embeddings = await embeddingGenerator.GenerateAsync(new[] 
{
    "Hello, world!",
    "This is a test.",
    "Local embeddings with Azure fallback"
});

foreach (var embedding in embeddings)
{
    Console.WriteLine($"Dimension: {embedding.Vector.Length}");
}
```

## How It Works

1. **Primary**: Attempts embedding generation with your local model
2. **Fallback**: If local generation fails (timeout, error, etc.), automatically switches to Azure OpenAI
3. **Retry**: Implements exponential backoff (configurable attempts)
4. **Logging**: Tracks fallback events when enabled

## Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Endpoint` | string | Required | Azure OpenAI endpoint URL |
| `ApiKey` | string | Required | Azure OpenAI API key |
| `DeploymentName` | string | Required | Embedding model deployment name |
| `MaxFallbackAttempts` | int | 3 | Number of Azure retry attempts |
| `TimeoutMilliseconds` | int | 30000 | Timeout for Azure requests |
| `LogFallbackEvents` | bool | true | Enable/disable fallback logging |

## Logging Events

When `LogFallbackEvents` is enabled, the generator logs:

- ✓ Successful local generation attempts
- ⚠️ Local generation failures
- 🔄 Fallback attempts to Azure (with attempt count)
- ✓ Successful Azure fallback responses
- ❌ Azure failures with error details

Configure logging:

```csharp
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});
```

## Security Best Practices

- **Never hardcode credentials** — use environment variables or Azure Key Vault
- **Store API keys securely** — never commit them to version control
- **Validate endpoints** — ensure URLs are from your Azure subscription
- **Use managed identities** where possible instead of API keys

## Architecture

```
┌────────────────────────────┐
│  HybridAzureEmbedding      │
│   Generator (Decorator)    │
└────────────┬───────────────┘
             │
    ┌────────┴────────┐
    │                 │
    ▼                 ▼
┌─────────────┐  ┌──────────────┐
│   Local     │  │   Azure      │
│ Embeddings  │  │   OpenAI     │
│   (Primary) │  │   (Fallback) │
└─────────────┘  └──────────────┘
```

## Examples

### Example 1: Simple Usage

```csharp
var texts = new[] { "Python", "JavaScript", "C#" };
var result = await embeddingGenerator.GenerateAsync(texts);
Console.WriteLine($"Generated {result.Count} embeddings");
```

### Example 2: With Error Handling

```csharp
try
{
    var embeddings = await embeddingGenerator.GenerateAsync(["test"]);
    Console.WriteLine("Embeddings generated successfully");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"All fallback attempts failed: {ex.Message}");
}
```

### Example 3: With Custom Configuration

```csharp
services.AddLocalEmbeddings();
services.AddLocalEmbeddingsWithAzureFallback(options =>
{
    options.Endpoint = azureEndpoint;
    options.ApiKey = azureKey;
    options.DeploymentName = deploymentName;
    options.MaxFallbackAttempts = 5;  // More attempts
    options.TimeoutMilliseconds = 60_000;  // Longer timeout
    options.LogFallbackEvents = true;
});
```

## Troubleshooting

### Local Generation Failing
- Ensure the ONNX model is properly initialized
- Check local system resources (memory, CPU)
- Review logs for detailed error messages

### Azure Fallback Not Working
- Verify Azure endpoint URL and API key
- Check deployment name matches your Azure configuration
- Ensure network connectivity to Azure
- Verify API key permissions

### Timeout Issues
- Increase `TimeoutMilliseconds` if requests take longer
- Reduce batch size for faster processing
- Check network latency to Azure

## See Also

- [ElBruno.LocalEmbeddings Core Library](https://github.com/elbruno/elbruno.localembeddings)
- [Azure OpenAI Service](https://learn.microsoft.com/azure/ai-services/openai/)
- [Microsoft.Extensions.AI](https://github.com/dotnet/extensions)
