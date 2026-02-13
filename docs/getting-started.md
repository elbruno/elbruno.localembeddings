# Getting Started — ElBruno.LocalEmbeddings

A step-by-step guide from your first embedding to full RAG with a local LLM.

## Step 1: Hello World — Generate Your First Embedding

Install the package and generate an embedding in a few lines:

```bash
dotnet new console -n EmbeddingDemo
cd EmbeddingDemo
dotnet add package ElBruno.LocalEmbeddings
```

```csharp
using ElBruno.LocalEmbeddings;
using ElBruno.LocalEmbeddings.Options;

// Create the generator (downloads model automatically on first run)
using var generator = new LocalEmbeddingGenerator(new LocalEmbeddingsOptions());

// Generate an embedding
var result = await generator.GenerateAsync(["Hello, world!"]);
float[] vector = result[0].Vector.ToArray();

Console.WriteLine($"Dimensions: {vector.Length}");           // 384
Console.WriteLine($"First 3 values: {vector[0]:F4}, {vector[1]:F4}, {vector[2]:F4}");
```

The first run downloads the default model (`sentence-transformers/all-MiniLM-L6-v2`) and caches it locally. Subsequent runs load instantly.

## Step 2: Compare Two Sentences — Cosine Similarity

Embeddings turn text into vectors. Similar texts produce vectors that point in similar directions. Cosine similarity measures this — 1.0 means identical, 0.0 means unrelated.

```csharp
using ElBruno.LocalEmbeddings.Extensions; // for CosineSimilarity

var embeddings = await generator.GenerateAsync(["I love programming", "I enjoy coding"]);
float similarity = embeddings[0].CosineSimilarity(embeddings[1]);

Console.WriteLine($"Similarity: {similarity:P1}"); // ~85%+
```

Try comparing unrelated sentences to see the difference:

```csharp
var embeddings = await generator.GenerateAsync(["I love programming", "The weather is nice today"]);
float similarity = embeddings[0].CosineSimilarity(embeddings[1]);

Console.WriteLine($"Similarity: {similarity:P1}"); // much lower
```

## Step 3: Batch Processing — Embed Multiple Documents

Process many texts in a single call for better throughput:

```csharp
var documents = new[]
{
    "Machine learning is a subset of artificial intelligence.",
    "JavaScript is widely used for web development.",
    "C# is developed by Microsoft for .NET applications."
};

var embeddings = await generator.GenerateAsync(documents);
Console.WriteLine($"Generated {embeddings.Count} embeddings, each with {embeddings[0].Vector.Length} dimensions");
```

Batching is more efficient than calling `GenerateAsync` once per string because the ONNX runtime can optimize memory and computation across the batch.

## Step 4: Semantic Search — Find Relevant Documents

Use `FindClosest` to search a collection by meaning instead of keywords:

```csharp
using ElBruno.LocalEmbeddings.Extensions;

// Build a searchable collection
var docs = new[]
{
    "Python for data science",
    "JavaScript for web apps",
    "C# for .NET development",
    "Rust for systems programming",
    "Swift for iOS development"
};
var docEmbeddings = await generator.GenerateAsync(docs);

// Pair each document with its embedding
var indexed = docs.Zip(docEmbeddings, (text, emb) => (text, emb)).ToList();

// Search by meaning
var query = await generator.GenerateAsync(["What language for building websites?"]);
var results = indexed.FindClosest(query[0], topK: 2, minScore: 0.3f);

foreach (var (text, score) in results)
    Console.WriteLine($"  {score:P0} — {text}");
// Output: JavaScript for web apps ranks highest
```

`FindClosest` computes cosine similarity against every item and returns the top matches above the minimum score threshold.

## Step 5: Dependency Injection — Use in Real Applications

Register LocalEmbeddings with `IServiceCollection` for production apps:

```csharp
using ElBruno.LocalEmbeddings.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddLocalEmbeddings(options =>
{
    options.ModelName = "sentence-transformers/all-MiniLM-L6-v2";
    options.MaxSequenceLength = 256;
});

var provider = services.BuildServiceProvider();
var generator = provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

var result = await generator.GenerateAsync(["DI works!"]);
Console.WriteLine($"Dimensions: {result[0].Vector.Length}");
```

The `AddLocalEmbeddings()` method registers:

- `IOptions<LocalEmbeddingsOptions>` — configuration
- `HttpClient` via `IHttpClientFactory` — for model downloads
- `IEmbeddingGenerator<string, Embedding<float>>` — the generator (singleton)

See [Dependency Injection docs](dependency-injection.md) for all registration overloads including `IConfiguration` binding from `appsettings.json`.

## Step 6: RAG — Combine with a Local LLM

Retrieval-Augmented Generation (RAG) combines embeddings-based search with an LLM to answer questions using your own data. The pattern:

```
User question
    → Embed the question (LocalEmbeddings)
    → Find similar documents (cosine similarity)
    → Build a prompt with the retrieved context
    → Send to LLM (phi-3.5-mini)
    → Stream the response
```

Two complete RAG samples are included:

| Sample | LLM Provider | How to Run |
|--------|-------------|------------|
| [RagOllama](../samples/RagOllama) | Ollama + `OllamaSharp` | `ollama pull phi3.5` then `dotnet run` |
| [RagFoundryLocal](../samples/RagFoundryLocal) | Foundry Local + OpenAI client | Install Foundry Local then `dotnet run` |

Both use `LocalEmbeddingGenerator` for embeddings and `IChatClient` (from `Microsoft.Extensions.AI`) for chat completions, so the RAG logic is identical — only the LLM provider changes.

> **Tip:** The [RagOllama](../samples/RagOllama) sample uses the companion package `ElBruno.LocalEmbeddings.KernelMemory` which integrates directly with [Microsoft Kernel Memory](https://github.com/microsoft/kernel-memory). Instead of building the RAG pipeline manually, the single call `.WithLocalEmbeddings()` on `KernelMemoryBuilder` handles embedding registration. See [Kernel Memory Integration](kernel-memory-integration.md) for details.

### Key code pattern (from both samples)

```csharp
// 1. Embed the user's question
var queryEmbedding = await embeddingGenerator.GenerateAsync([userQuestion]);

// 2. Find the most relevant documents
var results = vectorStore.Search(queryEmbedding[0], topK: 3);

// 3. Build a prompt with retrieved context
var context = string.Join("\n\n", results.Select(r => r.Content));
var prompt = $"""
    Answer the question using only the context below.

    Context:
    {context}

    Question: {userQuestion}
    """;

// 4. Stream the LLM response
await foreach (var chunk in chatClient.GetStreamingResponseAsync(prompt))
    Console.Write(chunk);
```

## What's Next?

- **[API Reference](api-reference.md)** — Full class and method documentation
- **[Configuration](configuration.md)** — All options, supported models, cache locations
- **[Dependency Injection](dependency-injection.md)** — All DI overloads and `IConfiguration` binding
- **[Kernel Memory Integration](kernel-memory-integration.md)** — Use local embeddings with Microsoft Kernel Memory for semantic memory / RAG
- **[Samples README](../samples/README.md)** — All sample projects with prerequisites and run instructions
