# ElBruno.LocalEmbeddings.BlazorComponents

Ready-to-use Blazor components for building embedding-powered web apps — semantic search, similarity meters, model galleries, and developer tooling — all backed by `ElBruno.LocalEmbeddings`.

## Installation

```bash
dotnet add package ElBruno.LocalEmbeddings.BlazorComponents
```

## Quick Start

**1. Register services in `Program.cs`:**

```csharp
using ElBruno.LocalEmbeddings.BlazorComponents;
using ElBruno.LocalEmbeddings.Extensions;

builder.Services.AddServerSideBlazor();
builder.Services.AddLocalEmbeddingsBlazorComponents();   // registers EmbeddingStateService (Scoped)
builder.Services.AddLocalEmbeddings(options =>
{
    options.ModelName = "sentence-transformers/all-MiniLM-L6-v2";
    options.EnsureModelDownloaded = true;
});
```

**2. Add the global import in `_Imports.razor`:**

```razor
@using ElBruno.LocalEmbeddings.BlazorComponents
@using ElBruno.LocalEmbeddings.BlazorComponents.Components
```

---

## Components

### Core — Model Management

#### `<EmbeddingModelStatusCard>`

Shows the download state of a single embedding model with progress bar and actions.

```razor
<EmbeddingModelStatusCard Model="@myModel"
                          OnDownload="@HandleDownload"
                          OnCancel="@HandleCancel"
                          OnDelete="@HandleDelete"
                          OnOpenFolder="@HandleOpenFolder"
                          IsCompact="false" />
```

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `Model` | `EmbeddingModelInfo` | ✅ | Model to display |
| `IsCompact` | `bool` | | Compact layout mode |
| `OnDownload` | `EventCallback<EmbeddingModelInfo>` | | Download button clicked |
| `OnCancel` | `EventCallback<EmbeddingModelInfo>` | | Cancel button clicked |
| `OnDelete` | `EventCallback<EmbeddingModelInfo>` | | Delete button clicked |
| `OnOpenFolder` | `EventCallback<EmbeddingModelInfo>` | | Open Folder clicked |

---

#### `<EmbeddingModelGallery>`

Filterable grid of embedding models backed by `EmbeddingModelStatusCard`.

```razor
@inject EmbeddingStateService EmbeddingState

<EmbeddingModelGallery Models="@EmbeddingState.Models"
                       OnDownload="@HandleDownload" />
```

**Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `Models` | `IReadOnlyList<EmbeddingModelInfo>` | Models to display |
| `OnDownload/Cancel/Delete/OpenFolder` | `EventCallback<EmbeddingModelInfo>` | Action callbacks |

---

#### `<EmbeddingModelSelector>`

Two-way-bindable `<select>` for picking an active model.

```razor
<EmbeddingModelSelector Models="@EmbeddingState.Models"
                        @bind-Value="_selectedModelId"
                        Disabled="false" />
```

---

### Developer Tools

#### `<EmbeddingExplorer>`

Enter 2–10 sentences, generate embeddings, and display a colour-coded cosine-similarity heatmap. Perfect for tuning RAG chunking strategies.

```razor
@inject IEmbeddingGenerator<string, Embedding<float>> Generator

<EmbeddingExplorer Generator="@Generator" />
```

---

#### `<SemanticSearchBox>`

A query input with ranked results backed by any `IEmbeddingGenerator`.

```razor
<SemanticSearchBox Generator="@Generator"
                   Corpus="@_documents"
                   MaxResults="5"
                   MinScore="0.2f"
                   Placeholder="Search knowledge base…" />
```

**Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Generator` | `IEmbeddingGenerator<string, Embedding<float>>` | | Embedding generator |
| `Corpus` | `IReadOnlyList<string>` | | Texts to search |
| `MaxResults` | `int` | `5` | Max results shown |
| `MinScore` | `float` | `0.0` | Minimum similarity threshold |
| `Placeholder` | `string` | `"Search…"` | Input placeholder |

---

#### `<EmbeddingDimensionViewer>`

2-D scatter plot (PCA projection) showing clustering of a set of embeddings.

```razor
<EmbeddingDimensionViewer Labels="@_labels"
                          Embeddings="@_embeddings"
                          Title="Sentence Clusters"
                          CanvasSize="400" />
```

**Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `Labels` | `IReadOnlyList<string>` | Display label per point |
| `Embeddings` | `IReadOnlyList<float[]>` | Pre-computed embedding vectors |
| `Title` | `string` | Chart title |
| `CanvasSize` | `int` | SVG canvas side length (px) |

---

#### `<SimilarityMeter>`

Paste two texts and see their cosine similarity as a colour gradient gauge (red → green).

```razor
<SimilarityMeter Generator="@Generator" />
```

---

### Infrastructure

#### `<EmbeddingHealthBadge>`

Compact status dot for nav-bars — green when ready, red when not.

```razor
<EmbeddingHealthBadge IsReady="@modelIsLoaded"
                      ModelName="all-MiniLM-L6-v2"
                      HideLabel="false" />
```

---

#### `<EmbeddingMetricsPanel>`

Live stats panel: tokens/sec, embedding dimension, batch size, memory usage.

```razor
<EmbeddingMetricsPanel TokensPerSecond="@_tokensPerSec"
                       EmbeddingDimension="384"
                       BatchSize="32"
                       MemoryUsageMb="@_memoryMb" />
```

---

## `EmbeddingStateService`

Scoped service registered by `AddLocalEmbeddingsBlazorComponents()`. Exposes:

| Member | Description |
|--------|-------------|
| `Models` | All well-known embedding models with state |
| `SelectedModelId` | Currently active model ID (two-way) |
| `SelectedModel` | Resolved `EmbeddingModelInfo` for the selected model |
| `SelectedModelChanged` | Event raised when selection changes |
| `CosineSimilarity(a, b)` | Static helper — cosine similarity between two float arrays |
| `GenerateEmbeddingsAsync(generator, texts)` | Static helper — batch embedding generation |

---

## Sample App

A working demo is included at `src/Samples/BlazorDemo/`. Run it:

```bash
cd src/Samples/BlazorDemo
dotnet run
```

Then open `https://localhost:5001` to see all 9 components in action.

---

## CSS

The library ships a bundled stylesheet. Reference it in your `_Host.cshtml` or `index.html`:

```html
<link rel="stylesheet" href="_content/ElBruno.LocalEmbeddings.BlazorComponents/ElBruno.LocalEmbeddings.BlazorComponents.bundle.scp.css" />
```
