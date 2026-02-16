# 🖼️ Local Image Embeddings in .NET — CLIP + ONNX, Zero Cloud Calls

Hi! 👋

If you’ve used `ElBruno.LocalEmbeddings` for **text** embeddings, you’re going to love the new **image** capabilities. The new `ElBruno.LocalEmbeddings.ImageEmbeddings` library brings **CLIP-based multimodal embeddings** to .NET — fully local, powered by ONNX Runtime, and ready for image search and image RAG workflows.

In this post, I’ll show you:

- How to download the required CLIP models
- A tiny “hello image embeddings” sample in C#
- The two image samples included in the repo: **ImageRagSimple** and **ImageRagChat**

Let’s dive in! 🚀

---

## 📦 The Library: Image Embeddings (CLIP)

The image embedding library is built on top of OpenAI’s **CLIP** model (Contrastive Language–Image Pretraining). It uses two ONNX models:

- **Text encoder** → embeds natural language queries
- **Vision encoder** → embeds images

Both embeddings live in the same vector space, which means **text-to-image** and **image-to-image** search works with simple cosine similarity.

> ✅ Everything runs locally. No cloud calls, no API keys.

---

## ⬇️ Download the CLIP Models

CLIP requires four files:

- `text_model.onnx`
- `vision_model.onnx`
- `vocab.json`
- `merges.txt`

We provide scripts that download the correct files from Hugging Face.

### Windows (PowerShell)

```powershell
./scripts/download_clip_models.ps1
```

### Linux / macOS (Bash)

```bash
chmod +x scripts/download_clip_models.sh
./scripts/download_clip_models.sh
```

These scripts download the models to:

```
./scripts/clip-models
```

---

## ✅ Basic Usage — Minimal C# Example

Here’s the simplest possible flow using the new library:

```csharp
using ElBruno.LocalEmbeddings.ImageEmbeddings;

string modelDir = "./scripts/clip-models";
string imageDir = "./samples/images";

string textModelPath = Path.Combine(modelDir, "text_model.onnx");
string visionModelPath = Path.Combine(modelDir, "vision_model.onnx");
string vocabPath = Path.Combine(modelDir, "vocab.json");
string mergesPath = Path.Combine(modelDir, "merges.txt");

using var textEncoder = new ClipTextEncoder(textModelPath, vocabPath, mergesPath);
using var imageEncoder = new ClipImageEncoder(visionModelPath);

var searchEngine = new ImageSearchEngine(imageEncoder, textEncoder);
searchEngine.IndexImages(imageDir);

var results = searchEngine.SearchByText("a cat", topK: 3);

foreach (var (imagePath, score) in results)
{
    Console.WriteLine($"{Path.GetFileName(imagePath)} → {score:F4}");
}
```

That’s it: index images → run text query → get ranked results.

---

## 🧪 Sample 1: ImageRagSimple

**ImageRagSimple** is the most minimal sample. It demonstrates the core flow:

1. Load CLIP text + vision models
2. Index all images in a folder
3. Run a few hardcoded text queries

Run it like this:

```bash
dotnet run --project samples/ImageRagSimple -- ./scripts/clip-models ./samples/images
```

This is the best sample to read if you want to understand the **library usage** with minimal noise.

---

## 💬 Sample 2: ImageRagChat

**ImageRagChat** builds on the same engine but adds a polished CLI experience using Spectre.Console. It supports:

- Live **text-to-image search**
- **Image-to-image search** with `image:<path>`
- A readable, interactive UI

Run it like this:

```bash
dotnet run --project samples/ImageRagChat -- --model-dir ./scripts/clip-models --image-dir ./samples/images
```

Commands inside the app:

- Type any text → search images
- Type `image: path/to/image.jpg` → image-to-image search
- Type `exit` → quit

---

## 🧭 Which Sample Should You Start With?

| Sample | Best For | Notes |
|--------|---------|-------|
| **ImageRagSimple** | Learning the library API | Straight-line demo, no UI |
| **ImageRagChat** | Interactive exploration | Great UX + supports image-to-image |

---

## 🎬 Video Walkthrough (Coming Soon)

I’m working on a short video demo that walks through the library and both samples — coming soon! 🎥

In the meantime, check out the videos on my channel:

👉 <https://www.youtube.com/elbruno>

---

## 📚 Resources

- [Image Embeddings setup guide](../../samples/README_IMAGES.md)
- [ImageRagSimple sample](../../samples/ImageRagSimple/Program.cs)
- [ImageRagChat sample](../../samples/ImageRagChat/Program.cs)

---

Happy coding! 👋

Greetings

**El Bruno**

---

More posts in my blog [ElBruno.com](https://elbruno.com).

More info in [https://beacons.ai/elbruno](https://beacons.ai/elbruno)
