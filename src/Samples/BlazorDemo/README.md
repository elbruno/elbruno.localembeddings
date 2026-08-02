# BlazorDemo — ElBruno.LocalEmbeddings Blazor Components Sample

A Blazor Server application that demonstrates all 9 components from the
`ElBruno.LocalEmbeddings.BlazorComponents` package in a single-page tour.

---

## Prerequisites

| Requirement | Version |
|-------------|---------|
| .NET SDK | 8.0 or later |
| Internet access | First run downloads `all-MiniLM-L6-v2` (~23 MB) |

No GPU required — everything runs on CPU via ONNX Runtime.

---

## Running the demo

```bash
# From the repo root:
cd src/Samples/BlazorDemo
dotnet run
```

Then open **https://localhost:5001** (or **http://localhost:5000**) in your browser.

> **First run:** The app downloads the `sentence-transformers/all-MiniLM-L6-v2` ONNX model
> to your local HuggingFace cache the first time it starts. Subsequent runs start instantly.

---

## What you'll see

The demo page walks through all 9 components in order:

| # | Section | Component | What it shows |
|---|---------|-----------|---------------|
| 1 | Similarity Meter | `<SimilarityMeter>` | Paste two texts → colour-gradient cosine similarity gauge |
| 2 | Embedding Explorer | `<EmbeddingExplorer>` | Enter 2–10 sentences → cosine-similarity heatmap |
| 3 | Semantic Search Box | `<SemanticSearchBox>` | Type a query → ranked results from a sample corpus |
| 4 | Model Selector | `<EmbeddingModelSelector>` | Dropdown of well-known models with two-way `@bind-Value` |
| 5 | Model Gallery | `<EmbeddingModelGallery>` | Filterable grid of model cards |
| 6 | Health Badge | `<EmbeddingHealthBadge>` | Compact nav-bar dot (green = ready, red = not loaded) |
| 7 | Metrics Panel | `<EmbeddingMetricsPanel>` | Live stats: tokens/sec, dimension, batch size, memory |
| 8 | Dimension Viewer | `<EmbeddingDimensionViewer>` | Click **Compute** → 2-D PCA scatter plot of the sample corpus |

---

## Project structure

```
BlazorDemo/
├── Program.cs              — DI setup: AddLocalEmbeddings + AddLocalEmbeddingsBlazorComponents
├── App.razor               — Root Blazor component
├── _Imports.razor          — Global @using directives
├── Pages/
│   ├── Index.razor         — Main demo page (all 9 components)
│   └── _Host.cshtml        — Server-side Blazor host page
└── Shared/
    └── MainLayout.razor    — Minimal layout
```

---

## Key code

### Registering services (`Program.cs`)

```csharp
using ElBruno.LocalEmbeddings.BlazorComponents;
using ElBruno.LocalEmbeddings.Extensions;

builder.Services.AddServerSideBlazor();

// Scoped EmbeddingStateService + well-known model catalogue
builder.Services.AddLocalEmbeddingsBlazorComponents();

// IEmbeddingGenerator<string, Embedding<float>> via ONNX Runtime
builder.Services.AddLocalEmbeddings(options =>
{
    options.ModelName = "sentence-transformers/all-MiniLM-L6-v2";
    options.EnsureModelDownloaded = true;  // downloads on first run
});
```

### Using a component (`Pages/Index.razor`)

```razor
@inject IEmbeddingGenerator<string, Embedding<float>> Generator

<SimilarityMeter Generator="@Generator" />

<EmbeddingDimensionViewer Labels="@myLabels"
                          Embeddings="@myEmbeddings"
                          Title="My clusters"
                          CanvasSize="400" />
```

---

## See also

- [ElBruno.LocalEmbeddings.BlazorComponents API reference](../../docs/blazor-components.md)
- [NuGet package](https://www.nuget.org/packages/ElBruno.LocalEmbeddings.BlazorComponents)
