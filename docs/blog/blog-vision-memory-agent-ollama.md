# Vision Memory Agent: local image search with CLIP + Ollama + Microsoft Agent Framework

This post walks through a tiny but powerful scenario: **ingest images locally, search them with natural language, and let an agent decide when to call tools**. Everything runs on your machine—no cloud calls, no external storage.

We’ll use:

- **CLIP** image embeddings (ONNX) for fast local vector search.
- **Ollama** as the local LLM runtime.
- **Microsoft Agent Framework** to wire tool-calling into a conversational agent.

> Repo sample: <https://github.com/elbruno/elbruno.localembeddings/tree/main/samples/VisionMemoryAgentSample>

## Why this scenario is fun

You give the agent an image path (or a folder), the agent stores embeddings locally, and later you ask it things like:

- “Find images similar to a beautiful sky at dusk.”
- “Ingest all images from a folder.”

No tags required. The embeddings do the work.

## Prerequisites

- .NET 10 SDK: <https://dotnet.microsoft.com/download>
- Ollama: <https://ollama.com/>
- Microsoft Agent Framework (Ollama provider):
  <https://learn.microsoft.com/en-us/agent-framework/agents/providers/ollama?pivots=programming-language-csharp>

## Step 1 — Run Ollama and pull a model

```bash
ollama serve
ollama pull llama3.2
```

## Step 2 — Download CLIP models locally

Follow the repo guide to download the required CLIP assets locally:

<https://github.com/elbruno/elbruno.localembeddings/blob/main/samples/README_IMAGES.md#1-setup-download-models>

## Step 3 — Run the sample

```bash
# From repo root
 dotnet run --project samples/VisionMemoryAgentSample -- --model-dir ./scripts/clip-models
```

## The agent wiring (core code)

The heart of the sample is **a local agent with function tools**. Here’s the key setup:

```csharp
IChatClient chatClient = new OllamaChatClient(new Uri("http://localhost:11434"), modelId: ollamaModel)
    .AsBuilder()
    .UseFunctionInvocation()
    .Build();

var tools = new[]
{
    AIFunctionFactory.Create(IngestImage),
    AIFunctionFactory.Create(FindSimilarImages),
    AIFunctionFactory.Create(IngestImagesFromFolder)
};

AIAgent agent = chatClient.AsAIAgent(
    name: "VisionMemoryAgent",
    instructions: """
        You are a Vision Memory agent. You help users manage and search a local image collection.
        You have three tools:
        - IngestImage: to add an image to the in-memory store
        - IngestImagesFromFolder: to add all images from a folder to the in-memory store
        - FindSimilarImages: to search stored images using natural language
        Always use the tools when the user asks to ingest or search images.
        Report tool results clearly.
        """,
    tools: [.. tools]);

AgentSession session = await agent.CreateSessionAsync();
```

### Why `UseFunctionInvocation()` matters

If you skip `UseFunctionInvocation()`, the model will **return raw JSON tool calls** instead of actually executing your tools. This one line is the bridge from “tool request” to “tool execution.”

## Tool 1 — Ingest a single image

```csharp
[Description("Ingest a local image file and store its CLIP embedding in memory. Returns confirmation.")]
string IngestImage(
    [Description("Absolute or relative path to the image file")] string path)
{
    if (!File.Exists(path))
        return $"Error: file not found: {path}";

    float[] embedding = imageEncoder.Encode(path);
    imageStore.Add((path, string.Empty, embedding));
    return $"Ingested '{Path.GetFileName(path)}'. Store now has {imageStore.Count} image(s).";
}
```

## Tool 2 — Ingest all images in a folder

```csharp
[Description("Ingest all the images from a local folder and store its CLIP embedding in memory. Returns confirmation.")]
string IngestImagesFromFolder(
    [Description("Absolute or relative path to the folder")] string folderPath)
{
    if (!Directory.Exists(folderPath))
        return $"Error: folder not found: {folderPath}";

    string[] extensions = [".jpg", ".jpeg", ".png", ".bmp", ".gif"];
    var imageFiles = Directory.GetFiles(folderPath)
        .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
        .ToList();

    if (imageFiles.Count == 0)
        return $"No images found in folder: {folderPath}";

    int ingestedCount = 0;
    foreach (string imagePath in imageFiles)
    {
        float[] embedding = imageEncoder.Encode(imagePath);
        imageStore.Add((imagePath, string.Empty, embedding));
        ingestedCount++;
    }

    return $"Ingested {ingestedCount} image(s) from '{folderPath}'. Store now has {imageStore.Count} image(s).";
}
```

## Tool 3 — Find similar images

```csharp
[Description("Find images similar to a natural language query using CLIP embeddings. Returns top matches.")]
string FindSimilarImages(
    [Description("Natural language search query")] string query,
    [Description("Number of top results to return")] int topK = 3)
{
    if (imageStore.Count == 0)
        return "No images in store. Ingest some images first.";

    float[] queryEmbedding = textEncoder.Encode(query);

    var results = imageStore
        .Select(img => (img.Path, img.Tags, Score: TensorPrimitives.CosineSimilarity(
            queryEmbedding.AsSpan(), img.Embedding.AsSpan())))
        .OrderByDescending(r => r.Score)
        .Take(topK)
        .ToList();

    var lines = results.Select((r, i) =>
        $"  {i + 1}. {Path.GetFileName(r.Path)} (score: {r.Score:F4})");

    return $"Top {results.Count} result(s):\n{string.Join("\n", lines)}";
}
```

## Demo: conversational flow

```
> Please ingest the image at ./samples/images/cat.jpg
Ingested 'cat.jpg'. Store now has 1 image(s).

> Ingest images from folder ./samples/images
Ingested 2 image(s) from './samples/images'. Store now has 3 image(s).

> Find images similar to "a beautiful sky at dusk"
Top 1 result(s):
  1. sunset.jpg (score: 0.2845)
```

## Summary

You now have a **fully local vision memory agent**:

- Embeddings from CLIP
- LLM coordination from Ollama
- Tool orchestration via Microsoft Agent Framework

The end result is a clean, fast, and privacy-friendly way to explore image retrieval without sending anything to the cloud.

---

## References

- Microsoft Agent Framework — Ollama provider: <https://learn.microsoft.com/en-us/agent-framework/agents/providers/ollama?pivots=programming-language-csharp>
- Agent Framework providers overview: <https://learn.microsoft.com/en-us/agent-framework/agents/providers/>
- Ollama homepage: <https://ollama.com/>
- Ollama quickstart: <https://docs.ollama.com/quickstart>
- .NET SDK download: <https://dotnet.microsoft.com/download>
- Repository: <https://github.com/elbruno/elbruno.localembeddings>
