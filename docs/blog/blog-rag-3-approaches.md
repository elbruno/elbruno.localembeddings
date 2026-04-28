# 🧠 Building RAG in .NET with Local Embeddings — 3 Approaches, Zero Cloud Calls

Hi! 👋

One of the questions I get most often is: **"Bruno, can I build a RAG (Retrieval-Augmented Generation) app in .NET without sending my data to the cloud?"**

The answer is a resounding **YES**. 🚀

In this post, I'll walk you through **three different ways** to build RAG applications using [ElBruno.LocalEmbeddings](https://github.com/elbruno/elbruno.localembeddings) — a .NET library that generates text embeddings **locally** using ONNX Runtime. No external API calls for embeddings. Everything runs on your machine.

Each approach uses a different level of abstraction:

| # | Sample | Pattern | LLM | Complexity |
|---|--------|---------|-----|------------|
| 1 | **RagChat** | Retrieval-only (no LLM) | None | VectorData + DI |
| 2 | **RagOllama** | Turnkey RAG | Ollama (phi4-mini) | Kernel Memory orchestrates everything |
| 3 | **RagFoundryLocal** | Manual RAG pipeline | Foundry Local (phi-4-mini) | Full control, core library only |

Let's dive in! 🏊‍♂️

---

## 📦 The Library: ElBruno.LocalEmbeddings

Before we start, here's the quick setup. The core NuGet package:

```bash
dotnet add package ElBruno.LocalEmbeddings
```

And the companion packages we'll use across the samples:

```bash
# For Microsoft.Extensions.VectorData integration (Sample 1)
dotnet add package ElBruno.LocalEmbeddings.VectorData

# For Microsoft Kernel Memory integration (Sample 2)
dotnet add package ElBruno.LocalEmbeddings.KernelMemory
```

The library implements `IEmbeddingGenerator<string, Embedding<float>>` from [Microsoft.Extensions.AI](https://devblogs.microsoft.com/dotnet/introducing-microsoft-extensions-ai-preview/), so it plugs into any .NET AI pipeline that uses that abstraction. It downloads and caches [HuggingFace sentence-transformer models](https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2) automatically — no manual model management needed.

> 💡 **Default model:** `sentence-transformers/all-MiniLM-L6-v2` — 384-dimensional embeddings, ~90 MB download, cached locally after first run.

---

## 🔍 Sample 1: RagChat — Semantic Search with VectorData (No LLM!)

**The idea:** Embed a set of FAQ documents, store them in an in-memory vector store, and let the user search by typing natural language queries. The system returns the most relevant documents ranked by cosine similarity. **No LLM is involved** — this is pure embedding-based retrieval.

This sample uses the `ElBruno.LocalEmbeddings.VectorData` companion package, which integrates with [Microsoft.Extensions.VectorData](https://learn.microsoft.com/dotnet/api/microsoft.extensions.vectordata) abstractions and includes a built-in `InMemoryVectorStore`.

### Step 1: Define the Document Model

First, we define a `Document` class using VectorData attributes:

```csharp
using Microsoft.Extensions.VectorData;

public sealed class Document
{
    [VectorStoreKey]
    public required string Id { get; init; }

    [VectorStoreData]
    public required string Title { get; init; }

    [VectorStoreData]
    public required string Content { get; init; }

    [VectorStoreVector(384, DistanceFunction = DistanceFunction.CosineSimilarity)]
    public ReadOnlyMemory<float> Vector { get; set; }

    [VectorStoreData]
    public string? Category { get; init; }
}
```

Notice the `[VectorStoreVector(384)]` attribute — that matches the 384 dimensions of the default MiniLM model. The `DistanceFunction.CosineSimilarity` tells the vector store how to rank results.

### Step 2: Wire Up DI and Load Documents

```csharp
using ElBruno.LocalEmbeddings.VectorData.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;

// Step 1: Configure DI
var services = new ServiceCollection();
services.AddLocalEmbeddingsWithInMemoryVectorStore(options =>
{
    options.ModelName = "sentence-transformers/all-MiniLM-L6-v2";
    options.MaxSequenceLength = 256;
    options.EnsureModelDownloaded = true;
})
.AddVectorStoreCollection<string, Document>("faq");

using var serviceProvider = services.BuildServiceProvider();

// Step 2: Resolve embedding generator + vector collection
var embeddingGenerator = serviceProvider
    .GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
var faqCollection = serviceProvider
    .GetRequiredService<VectorStoreCollection<string, Document>>();
```

One line — `AddLocalEmbeddingsWithInMemoryVectorStore()` — registers both the local embedding generator **and** the in-memory vector store. Then we add a typed collection called `"faq"` for our `Document` model.

### Step 3: Batch Embed and Upsert

```csharp
// Step 3: Load FAQ documents, batch-embed, upsert into vector store
var documents = SampleData.GetFaqDocuments(); // 20 FAQ docs
var embeddings = await embeddingGenerator
    .GenerateAsync(documents.Select(d => d.Content).ToList());

for (var i = 0; i < documents.Count; i++)
    documents[i].Vector = embeddings[i].Vector;

await faqCollection.UpsertAsync(documents);
```

We batch-embed all 20 documents at once (efficient!), assign vectors, and upsert them into the vector store.

### Step 4: Search Loop

```csharp
while (true)
{
    var input = Console.ReadLine();

    // Embed the user query
    var queryEmbedding = (await embeddingGenerator.GenerateAsync([input]))[0];

    // Search the vector store
    var results = await faqCollection
        .SearchAsync(queryEmbedding, top: 3)
        .ToListAsync();

    // Filter by minimum similarity score
    results = results
        .Where(r => (r.Score ?? 0d) >= 0.2d)
        .OrderByDescending(r => r.Score ?? 0d)
        .ToList();

    foreach (var result in results)
        Console.WriteLine($"  [{result.Score:P0}] {result.Record.Title}");
}
```

That's it! The user types a question, we embed it, search the vector collection with `SearchAsync`, and display matches with their similarity scores. No LLM, no cloud calls, no API keys.

> 🎯 **Best for:** FAQ systems, documentation search, knowledge bases where you want fast retrieval without the overhead (or cost) of an LLM.

---

## 🦙 Sample 2: RagOllama — Full RAG with Kernel Memory + Ollama

**The idea:** Use [Microsoft Kernel Memory](https://github.com/microsoft/kernel-memory) to orchestrate the entire RAG pipeline — chunking, embedding, storage, retrieval, prompt building, and LLM response — with a single `.WithLocalEmbeddings()` call for the embedding part and [Ollama](https://ollama.com/) running `phi4-mini` locally for text generation.

This is the **"turnkey" approach** — Kernel Memory handles everything. You just import text and ask questions.

### The Before/After Pattern

This sample first asks the question **without** any memory (baseline), then asks the same question **with** RAG to show the difference:

```csharp
using ElBruno.LocalEmbeddings.KernelMemory.Extensions;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI.Ollama;
using Microsoft.KernelMemory.Configuration;
using OllamaSharp;

var ollamaEndpoint = "http://localhost:11434";
var modelIdChat = "phi4-mini";
var question = "What is Bruno's favourite super hero?";

// ❌ Ask WITHOUT memory — the model doesn't know the answer
var ollama = new OllamaApiClient(ollamaEndpoint)
{
    SelectedModel = modelIdChat
};
Console.WriteLine("Answer WITHOUT memory:");
await foreach (var token in ollama.GenerateAsync(question))
    Console.Write(token.Response);
```

Without context, the LLM just guesses. Now let's build the RAG pipeline:

### Build Kernel Memory with Local Embeddings

```csharp
// Configure Ollama for text generation
var config = new OllamaConfig
{
    Endpoint = ollamaEndpoint,
    TextModel = new OllamaModelConfig(modelIdChat)
};

// Build Kernel Memory: Ollama for chat + local embeddings for vectors
var memory = new KernelMemoryBuilder()
    .WithOllamaTextGeneration(config)
    .WithLocalEmbeddings()  // 👈 This is the magic line!
    .WithCustomTextPartitioningOptions(new TextPartitioningOptions
    {
        MaxTokensPerParagraph = 256,
        OverlappingTokens = 50
    })
    .Build();
```

`.WithLocalEmbeddings()` is an extension method from the `ElBruno.LocalEmbeddings.KernelMemory` companion package. Under the hood, it creates a `LocalEmbeddingGenerator` with default options and wraps it in a `LocalEmbeddingTextGenerator` adapter that implements Kernel Memory's `ITextEmbeddingGenerator` interface. One line, zero configuration.

### Import Facts and Ask with Memory

```csharp
// Import facts into memory
var facts = new[]
{
    "Gisela's favourite super hero is Batman",
    "Gisela watched Venom 3 2 weeks ago",
    "Bruno's favourite super hero is Invincible",
    "Bruno went to the cinema to watch Venom 3",
    "Bruno doesn't like the super hero movie: Eternals",
    "ACE and Goku watched the movies Venom 3 and Eternals",
};

for (var i = 0; i < facts.Length; i++)
    await memory.ImportTextAsync(facts[i], (i + 1).ToString());

// ✅ Ask WITH memory — now the model knows!
Console.WriteLine("\nAnswer WITH memory:");
await foreach (var result in memory.AskStreamingAsync(question))
{
    Console.Write(result.Result);
    if (result.RelevantSources.Count > 0)
        foreach (var source in result.RelevantSources)
            Console.WriteLine($"  [source: #{source.Index}] {source.SourceUrl}");
}
```

When you call `ImportTextAsync`, Kernel Memory automatically:

1. **Chunks** the text (256 tokens per paragraph, 50 overlapping)
2. **Embeds** each chunk using our local ONNX model
3. **Stores** the chunks and vectors in its built-in store

When you call `AskStreamingAsync`, it:

1. **Embeds** the question
2. **Retrieves** the most relevant chunks
3. **Builds** a prompt with the context
4. **Streams** the LLM response from Ollama

All in one call. The answer now correctly says "Bruno's favourite super hero is Invincible" — with source citations! 🎉

> 🎯 **Best for:** When you want RAG with minimal code. Kernel Memory handles chunking, storage, retrieval, and prompting. You focus on your data and questions.

### Prerequisites

- [Ollama](https://ollama.com/) running locally with `phi4-mini` pulled:

  ```bash
  ollama pull phi4-mini
  ```

---

## 🏗️ Sample 3: RagFoundryLocal — Manual RAG with Foundry Local

**The idea:** Build the entire RAG pipeline **by hand** — embed facts, search with `FindClosest()`, construct a prompt template, and stream the LLM response. This sample uses only the core `ElBruno.LocalEmbeddings` package (no companion packages) and [Microsoft AI Foundry Local](https://learn.microsoft.com/ai/foundry/foundry-local/get-started) for the LLM.

This is the **"full control" approach** — every step is explicit.

### Start the Model and Ask Without Context

```csharp
using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Extensions;
using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;

var modelAlias = "phi-4-mini";
var question = "What is Bruno's favourite super hero?";
const int topK = 3;

// Start Foundry Local model
await using var manager = await FoundryLocalManager.StartModelAsync(modelAlias);

// Resolve the alias to the actual model ID registered on the server
var modelIdChat = await ResolveModelIdAsync(manager.Endpoint, modelAlias);

var openAiClient = new OpenAIClient(
    new ApiKeyCredential(manager.ApiKey),
    new OpenAIClientOptions { Endpoint = manager.Endpoint });
IChatClient chatClient = openAiClient
    .GetChatClient(modelIdChat)
    .AsIChatClient();

// ❌ Ask without context (baseline)
await foreach (var update in chatClient.GetStreamingResponseAsync(
    [new ChatMessage(ChatRole.User, question)]))
    Console.Write(update.Text);
```

[Foundry Local](https://learn.microsoft.com/ai/foundry/foundry-local/get-started) starts a local inference server and exposes an OpenAI-compatible API. We use `IChatClient` from Microsoft.Extensions.AI — the same abstraction you'd use with Azure OpenAI or any other provider.

### Build the RAG Pipeline Step by Step

```csharp
// Same facts as the Ollama sample
string[] facts =
[
    "Gisela's favourite super hero is Batman",
    "Gisela watched Venom 3 2 weeks ago",
    "Bruno's favourite super hero is Invincible",
    "Bruno went to the cinema to watch Venom 3",
    "Bruno doesn't like the super hero movie: Eternals",
    "ACE and Goku watched the movies Venom 3 and Eternals",
];

// Step 1: Embed all facts locally
using var embeddingGenerator = new LocalEmbeddingGenerator();
var factEmbeddings = await embeddingGenerator.GenerateAsync(facts);

// Step 2: Zip facts with their embeddings
var indexedFacts = facts.Zip(
    factEmbeddings,
    (fact, embedding) => (Item: fact, Embedding: embedding));

// Step 3: Embed the question and find closest matches
var queryEmbedding = await embeddingGenerator.GenerateEmbeddingAsync(question);
var contextDocs = indexedFacts
    .FindClosest(queryEmbedding, topK: topK)
    .Select(match => match.Item);
```

Here we use two key extension methods from the core library:

- **`GenerateEmbeddingAsync(string)`** — convenience method that returns a single `Embedding<float>` directly (no array indexing needed)
- **`FindClosest()`** — extension on `IEnumerable<(T Item, Embedding<float>)>` that performs cosine similarity ranking and returns the top-K matches

No vector store, no DI container — just LINQ and extension methods.

### Build the Prompt and Stream the Response

```csharp
// Step 4: Build the prompt with retrieved context
static string BuildPrompt(string question, IEnumerable<string> contextDocs)
{
    var context = string.Join("\n- ", contextDocs);
    return $"""
        You are a helpful assistant. Use the provided context
        to answer briefly and accurately.

        Context:
        - {context}

        Question: {question}
        """;
}

// Step 5: Ask the LLM with context ✅
await foreach (var update in chatClient.GetStreamingResponseAsync(
    [new ChatMessage(ChatRole.User, BuildPrompt(question, contextDocs))]))
    Console.Write(update.Text);
```

We build a simple prompt template using C# raw string literals, inject the retrieved context, and stream the response. The LLM now has the relevant facts and answers correctly.

> 🎯 **Best for:** When you need full control over every step — custom prompt templates, custom retrieval logic, or when you want to understand exactly what's happening in the RAG pipeline.

### Prerequisites

- [Foundry Local CLI](https://learn.microsoft.com/ai/foundry/foundry-local/get-started) installed with `phi-4-mini` available:

  ```bash
  foundry model run phi-4-mini
  ```

---

## 📊 Comparison: Which Approach Should You Use?

| Aspect | RagChat | RagOllama | RagFoundryLocal |
|--------|---------|-----------|-----------------|
| **LLM** | None (retrieval only) | Ollama phi4-mini | Foundry Local phi-4-mini |
| **Embedding integration** | DI + VectorData | Kernel Memory companion | Core library directly |
| **RAG orchestration** | Manual (VectorData `SearchAsync`) | Automatic (Kernel Memory) | Manual (embed → search → prompt) |
| **Vector store** | `InMemoryVectorStore` (built-in) | Kernel Memory's built-in store | In-memory via LINQ |
| **Companion packages** | `ElBruno.LocalEmbeddings.VectorData` | `ElBruno.LocalEmbeddings.KernelMemory` | None — core only |
| **Key extension method** | `AddLocalEmbeddingsWithInMemoryVectorStore()` | `.WithLocalEmbeddings()` | `FindClosest()` |
| **Lines of RAG code** | ~20 | ~15 | ~25 |
| **Best for** | Search-only, FAQ, no LLM cost | Turnkey RAG with minimal code | Full pipeline control |

**My recommendation:**

- Start with **RagChat** if you just need semantic search and don't want an LLM dependency
- Use **RagOllama** if you want a complete RAG system with minimal plumbing
- Go with **RagFoundryLocal** if you need to customize every step of the pipeline

All three share the same foundation: **embeddings generated locally on your machine, no cloud calls, no API keys for the embedding part.**

---

## 🔗 References and Resources

### Project

- [ElBruno.LocalEmbeddings — GitHub Repository](https://github.com/elbruno/elbruno.localembeddings)
- [ElBruno.LocalEmbeddings — NuGet Package](https://www.nuget.org/packages/ElBruno.LocalEmbeddings)
- [ElBruno.LocalEmbeddings.KernelMemory — NuGet Package](https://www.nuget.org/packages/ElBruno.LocalEmbeddings.KernelMemory)
- [ElBruno.LocalEmbeddings.VectorData — NuGet Package](https://www.nuget.org/packages/ElBruno.LocalEmbeddings.VectorData)

### Sample Source Code

- [RagChat sample](https://github.com/elbruno/elbruno.localembeddings/tree/main/src/Samples/RagChat) — VectorData + semantic search
- [RagOllama sample](https://github.com/elbruno/elbruno.localembeddings/tree/main/src/Samples/RagOllama) — Kernel Memory + Ollama
- [RagFoundryLocal sample](https://github.com/elbruno/elbruno.localembeddings/tree/main/src/Samples/RagFoundryLocal) — Manual pipeline + Foundry Local

### External Projects

- [Microsoft.Extensions.AI](https://devblogs.microsoft.com/dotnet/introducing-microsoft-extensions-ai-preview/) — Unified AI abstractions for .NET
- [Microsoft.Extensions.VectorData](https://learn.microsoft.com/dotnet/api/microsoft.extensions.vectordata) — Vector store abstractions
- [Microsoft Kernel Memory](https://github.com/microsoft/kernel-memory) — RAG pipeline orchestration
- [Ollama](https://ollama.com/) — Run LLMs locally
- [Microsoft AI Foundry Local](https://learn.microsoft.com/ai/foundry/foundry-local/get-started) — Run AI models locally with OpenAI-compatible APIs
- [sentence-transformers/all-MiniLM-L6-v2](https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2) — Default embedding model (HuggingFace)

---

Happy coding! 👋

Greetings

**El Bruno**

---

More posts in my blog [ElBruno.com](https://elbruno.com).

More info in [https://beacons.ai/elbruno](https://beacons.ai/elbruno)

