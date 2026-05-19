# ElBruno.LocalEmbeddings.Azure

Azure OpenAI fallback integration for **ElBruno.LocalEmbeddings** — try local embeddings first, automatically fall back to Azure OpenAI on failure.

This optional companion package allows you to build resilient embedding generation pipelines that degrade gracefully when local models encounter issues.

## Features

- **Hybrid Mode**: Try local embeddings first, automatically fall back to Azure OpenAI
- **Configurable Retries**: Control fallback attempts and timeouts
- **Logging Support**: Track when and why fallbacks occur
- **Zero Breaking Changes**: Purely additive, doesn't modify the core library
- **Dependency Injection Ready**: Simple `AddLocalEmbeddingsWithAzureFallback()` registration

## Installation

```bash
dotnet add package ElBruno.LocalEmbeddings.Azure
```

## Quick Start

### 1. Configure Services

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
    options.ApiKey = "your-api-key";
    options.DeploymentName = "text-embedding-3-small";
    options.MaxFallbackAttempts = 3;
    options.TimeoutMilliseconds = 30_000;
});

var provider = services.BuildServiceProvider();
var generator = provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
```

### 2. Generate Embeddings

```csharp
// This automatically tries local first, falls back to Azure on error
var embeddings = await generator.GenerateAsync(new[] { "Hello, world!" });

foreach (var embedding in embeddings)
{
    Console.WriteLine($"Embedding: {string.Join(", ", embedding.Vector.Take(5))}...");
}
```

### 3. Use Configuration (appsettings.json)

Alternatively, load configuration from `appsettings.json`:

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

```csharp
services.AddLocalEmbeddingsWithAzureFallback("AzureEmbeddings");
```

## Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Endpoint` | string | Required | Azure OpenAI endpoint URL |
| `ApiKey` | string | Required | Azure OpenAI API key |
| `DeploymentName` | string | Required | Name of the embedding model deployment |
| `MaxFallbackAttempts` | int | 3 | Maximum number of Azure fallback attempts |
| `TimeoutMilliseconds` | int | 30000 | Timeout for Azure requests |
| `LogFallbackEvents` | bool | true | Whether to log fallback events |

## How It Works

1. **Primary**: Attempts embedding generation using your configured local model
2. **Fallback**: On any exception, automatically switches to Azure OpenAI
3. **Retry Logic**: Retries failed Azure requests up to `MaxFallbackAttempts` times with exponential backoff
4. **Logging**: When enabled, logs detailed information about fallback events

## Example: With Logging

```csharp
using Microsoft.Extensions.Logging;

var services = new ServiceCollection();
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

services.AddLocalEmbeddings();
services.AddLocalEmbeddingsWithAzureFallback(options =>
{
    options.Endpoint = "https://my-resource.openai.azure.com";
    options.ApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");
    options.DeploymentName = "text-embedding-3-small";
    options.LogFallbackEvents = true;
});

var provider = services.BuildServiceProvider();
var generator = provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

// Logs will show:
// - When local generation fails
// - When falling back to Azure
// - Success/failure of each fallback attempt
var embeddings = await generator.GenerateAsync(new[] { "test" });
```

## Requirements

- .NET 8.0 or .NET 10.0
- `ElBruno.LocalEmbeddings` package
- Azure OpenAI resource with an embedding model deployment

## Security Notes

- **Never hardcode credentials**: Use environment variables, Azure Key Vault, or configuration managers
- **API Keys**: Store API keys securely; never commit them to source control
- **Endpoint Validation**: Ensure the endpoint URL is from your Azure subscription

## License

MIT — See LICENSE file in the repository root.

## See Also

- [ElBruno.LocalEmbeddings](https://github.com/elbruno/elbruno.localembeddings)
- [Azure OpenAI API Documentation](https://learn.microsoft.com/azure/ai-services/openai/)
- [Microsoft.Extensions.AI](https://github.com/dotnet/extensions)
