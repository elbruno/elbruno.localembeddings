# Image Embeddings — Samples & Setup

This guide covers the setup and usage for all image-related samples in the `ElBruno.LocalEmbeddings` repository.

## Overview

The `ElBruno.LocalEmbeddings.ImageEmbeddings` library brings multimodal capabilities using OpenAI's CLIP (Contrastive Language–Image Pretraining) model via ONNX Runtime. This allows you to perform:

* **Text-to-Image Search:** Find images using natural language queries (e.g., "a dog running in the grass").
* **Image-to-Image Search:** Find images similar to another image.
* **Zero-Shot Classification:** Classify images without training a custom model.

## Available Samples

| Sample | Description | Key Features |
| :--- | :--- | :--- |
| **[ImageRagSimple](ImageRagSimple/Program.cs)** | Minimal Console App | Basic indexing and search loop. Good start for understanding the API. |
| **[ImageRagChat](ImageRagChat/Program.cs)** | Interactive CLI | Rich UI using Spectre.Console. Supports text and image queries. |
| **[ImageSearchSample](ImageSearchSample/Program.cs)** | Legacy / Standalone | Detailed look at the internal tokenizer/encoder logic (useful for learning). |

---

## 1. Setup: Download Models

Unlike the text-only models, the CLIP models consist of multiple large files that must be present on disk. You can download them easily using the provided helper scripts.

### Option A: Using Helper Scripts (Recommended)

**Windows (PowerShell)**

```powershell
./scripts/download_clip_models.ps1
```

* Downloads models to `./scripts/clip-models`.

**Linux / macOS (Bash)**

```bash
chmod +x scripts/download_clip_models.sh
./scripts/download_clip_models.sh
```

* Downloads models to `./scripts/clip-models`.

### Option B: Automatic Download (Library Feature)

You can configure the library to download models automatically on first run by setting `EnsureModelDownloaded = true` in your code.

```csharp
services.AddImageEmbeddings(options =>
{
    options.EnsureModelDownloaded = true;
    options.ModelDirectory = Path.Combine(Directory.GetCurrentDirectory(), "clip-models");
});
```

### Required Files

If you are managing models manually, your directory must contain:

1. `text_model.onnx` (Text Encoder)
2. `vision_model.onnx` (Image Encoder)
3. `vocab.json` (Tokenizer vocabulary)
4. `merges.txt` (Tokenizer merges)

---

## 2. Setup: Sample Images

We provide a set of sample images in `samples/images` to help you get started quickly.
These include:

* Animals (cat, dog, fox)
* Scenery (beach, mountains, forest)
* Objects (pizza, car, bicycle)

You can point any of the samples to this directory to index and search them.

---

## 3. Running the Samples

All samples require two arguments:

1. Path to the **model directory** (e.g., `./scripts/clip-models`)
2. Path to the **image directory** (e.g., `./samples/images`)

If you ran one of the download scripts (default location) and want to use the sample images, you can use these command line commands directly:

```bash
dotnet run --project samples/ImageRagSimple -- --model-dir ./scripts/clip-models --image-dir ./samples/images
```

```bash
dotnet run --project samples/ImageRagChat -- --model-dir ./scripts/clip-models --image-dir ./samples/images
```

```bash
dotnet run --project samples/ImageSearchSample -- --model-dir ./scripts/clip-models --image-dir ./samples/images
```

### Running ImageRagSimple

A bare-bones example that indexes images and runs a few hardcoded queries ("a cat", "sunset").

```bash
dotnet run --project samples/ImageRagSimple -- --model-dir ./scripts/clip-models --image-dir ./samples/images
```

### Running ImageRagChat

An interactive chat application. Type queries to search your images in real-time.

```bash
dotnet run --project samples/ImageRagChat -- --model-dir ./scripts/clip-models --image-dir ./samples/images
```

**Commands:**

* Type any text to search (e.g., "fast car")
* Type `image: path/to/image.jpg` to find similar images
* Type `exit` to quit

### Running ImageSearchSample

```bash
dotnet run --project samples/ImageSearchSample -- --model-dir ./scripts/clip-models --image-dir ./samples/images
```

---

## Troubleshooting

**"Required file not found..."**

* Ensure you ran the download script.
* Check that the path passed to the application matches where you downloaded the models.

**"No images found..."**

* Ensure the image path is correct (e.g., `./samples/images`).
* Supported formats: `.jpg`, `.jpeg`, `.png`, `.bmp`, `.gif`.

**"System.DllNotFoundException: onnxruntime"**

* Ensure you have the Visual C++ Redistributable installed (on Windows) or appropriate C++ runtime on Linux.
