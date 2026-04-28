# ElBruno.LocalEmbeddings — Improvement Scenarios

Prioritized scenarios for improving developer experience, based on analysis from three AI agents (Codex, Opus, Gemini) building samples with the library.

---

## Scenario 1: Built-in InMemoryVectorStore for VectorData Package

**Priority:** 🔴 High
**Effort:** Medium (3–5 days)
**Origin:** CODEX-1

### Problem

All three AI agents struggled with the VectorData sample because there is no lightweight in-memory `VectorStore` implementation available without pulling in Semantic Kernel connectors. Codex hand-rolled ~250 lines of custom infrastructure. Opus fell back to `Microsoft.SemanticKernel.Connectors.InMemory` (which drags in all of SK). Gemini gave up and shipped a commented-out placeholder.

This is the **#1 friction point** for new developers trying the VectorData integration.

### Proposed Solution

Ship a minimal `InMemoryVectorStore` implementation inside `ElBruno.LocalEmbeddings.VectorData` (or as a separate `ElBruno.LocalEmbeddings.VectorData.InMemory` companion package). It should:

- Implement `VectorStore` and `VectorStoreCollection<TKey, TRecord>` from `Microsoft.Extensions.VectorData`
- Support cosine similarity search via `SearchAsync<TInput>`
- Auto-discover `[VectorStoreKey]`, `[VectorStoreData]`, and `[VectorStoreVector]` attributes via reflection
- Be thread-safe using `ConcurrentDictionary`
- Require zero external dependencies beyond `Microsoft.Extensions.VectorData.Abstractions`

### Expected Impact

- Getting-started time for VectorData drops from "hours of research" to "5 minutes"
- Samples become self-contained and copy-pasteable
- Removes the misleading need to reference Semantic Kernel packages

### Example Usage

```csharp
using ElBruno.LocalEmbeddings.VectorData.Extensions;

var services = new ServiceCollection();
services.AddLocalEmbeddingsWithInMemoryVectorStore(options =>
{
    options.ModelName = "sentence-transformers/all-MiniLM-L6-v2";
});
services.AddVectorStoreCollection<int, ProductRecord>("products");
```

---

## Scenario 2: FindClosest Convenience API

**Priority:** 🔴 High
**Effort:** Low (1–2 days)
**Origin:** CODEX-2

### Problem

Every sample that performs semantic search repeats the same 8–10 line LINQ pattern:

```csharp
var queryEmbedding = await generator.GenerateEmbeddingAsync(query);
var results = knowledgeBase
    .Select((doc, idx) => new { Document = doc, Score = queryEmbedding.CosineSimilarity(kbEmbeddings[idx]) })
    .OrderByDescending(r => r.Score)
    .Take(3)
    .ToList();
```

This is error-prone (index alignment between text corpus and embeddings list), verbose, and repeated in every single consumer.

### Proposed Solution

Add convenience methods as extensions on `LocalEmbeddingGenerator` and/or on the embeddings collections:

```csharp
// Option A: Direct on generator — most ergonomic
var results = await generator.FindClosestAsync(
    query: "What is .NET Aspire?",
    corpus: knowledgeBase,           // IReadOnlyList<string>
    corpusEmbeddings: kbEmbeddings,  // pre-computed, optional
    topK: 3,
    minScore: 0.3f);

// Each result contains: Text, Index, Score
foreach (var match in results)
    Console.WriteLine($"{match.Score:F3} — {match.Text}");
```

```csharp
// Option B: Extension on embedding — for pre-computed scenarios
var closest = queryEmbedding.FindClosest(kbEmbeddings, topK: 3);
```

### Design Considerations

- If `corpusEmbeddings` is null, the method generates them on the fly (convenience for small datasets)
- Return type should be a simple `record`: `SemanticSearchResult(string Text, int Index, float Score)`
- Consider an overload that accepts `IReadOnlyList<T>` with a `Func<T, string>` selector for typed objects

### Expected Impact

- Reduces the most common usage pattern from ~10 lines to 1 line
- Eliminates index-alignment bugs
- Makes the library feel "batteries included" for the semantic search use case

---

## Scenario 3: GenerateAndUpsert Pipeline Helper for VectorData

**Priority:** 🟡 Medium
**Effort:** Low (1–2 days)
**Origin:** CODEX-3

### Problem

Every VectorData sample repeats a multi-step pipeline to insert records:

```csharp
// Step 1: Collect descriptions
var descriptions = recipes.Select(r => r.Description).ToArray();

// Step 2: Batch generate embeddings
var embeddings = await generator.GenerateAsync(descriptions);

// Step 3: Manually pair records with their vectors
for (var i = 0; i < recipes.Length; i++)
{
    var record = new RecipeRecord
    {
        Id = recipes[i].Id,
        Name = recipes[i].Name,
        Description = recipes[i].Desc,
        Vector = embeddings[i].Vector   // manually assign
    };
    await collection.UpsertAsync(record);
}
```

This is tedious, error-prone (index alignment again), and obscures the developer's intent.

### Proposed Solution

Add an extension method on `VectorStoreCollection` that integrates with `IEmbeddingGenerator`:

```csharp
// Single record — auto-generates embedding from the text field
await collection.UpsertWithEmbeddingAsync(generator, record, r => r.Description);

// Batch — generates embeddings for all records and upserts them
await collection.UpsertWithEmbeddingsAsync(generator, records, r => r.Description);
```

### Design Considerations

- The `Func<TRecord, string>` parameter extracts the text to embed from each record
- The method auto-assigns the generated vector to the `[VectorStoreVector]` property via reflection (or a second lambda `(record, vector) => record.Vector = vector`)
- Batch version should use `generator.GenerateAsync(texts)` for efficiency (single ONNX inference call)
- This should live in `ElBruno.LocalEmbeddings.VectorData` as an extension method

### Expected Impact

- Reduces the embed-and-insert pattern from ~10 lines to 1 line
- Naturally encourages batched embedding generation (better performance)
- Pairs well with Scenario 1 (InMemoryVectorStore) for a complete "zero boilerplate" getting-started experience

---

## Scenario 4: Microsoft.Extensions.AI Evaluation Integration

**Priority:** 🟡 Medium
**Effort:** Medium (3–5 days)
**Origin:** OPUS-4

### Problem

Developers using local embeddings for RAG or semantic search have no built-in way to measure embedding quality — e.g., does the model return the right documents for a set of test queries? There's no guidance on how to evaluate precision, recall, or relevance.

The `Microsoft.Extensions.AI.Evaluation` namespace provides a growing set of abstractions for evaluating AI components, but there's no integration or sample showing how to use it with local embeddings.

### Proposed Solution

Create a sample and optional helper utilities that integrate with `Microsoft.Extensions.AI.Evaluation`:

1. **Evaluation sample** (`src/src/Samples/EvaluationSample`):
   - Define a test dataset: `(query, expectedDocumentIds[])`
   - Run semantic search against the corpus
   - Measure retrieval metrics: Precision@K, Recall@K, Mean Reciprocal Rank (MRR)
   - Output a report showing per-query and aggregate scores

2. **Optional evaluator class** (in core package or companion):
   ```csharp
   var evaluator = new EmbeddingRetrievalEvaluator(generator);
   var report = await evaluator.EvaluateAsync(
       corpus: documents,
       testCases: new[]
       {
           new TestCase("Italian food", ExpectedIds: [1, 5]),
           new TestCase("Asian cuisine", ExpectedIds: [2, 6]),
       },
       topK: 3);

   Console.WriteLine($"Precision@3: {report.Precision:P1}");
   Console.WriteLine($"MRR:         {report.MeanReciprocalRank:F3}");
   ```

### Design Considerations

- Align with `Microsoft.Extensions.AI.Evaluation` interfaces as they stabilize
- Keep the evaluator lightweight — no ML dependencies, just metric math
- Consider supporting model comparison (run the same test suite against different models)

### Expected Impact

- Enables data-driven model selection (all-MiniLM-L6-v2 vs L12-v2 vs others)
- Provides a testing pattern for CI/CD pipelines that use embeddings
- Positions the library as production-grade, not just demo-grade

---

## Scenario 5: TagCollection & Metadata Filtering Documentation

**Priority:** 🟡 Medium
**Effort:** Low (1–2 days)
**Origin:** GEMINI-1

### Problem

The Kernel Memory integration supports `TagCollection` for metadata-aware filtering during search, but this is not documented or demonstrated in any existing sample. Gemini was the only agent that used it:

```csharp
await memory.ImportTextAsync(text,
    documentId: "doc002",
    tags: new TagCollection { { "category", "travel" }, { "city", "tokyo" } });
```

Filtered search (e.g., "search only in category=travel") is critical for multi-tenant or multi-domain RAG applications, and developers won't discover it without guidance.

### Proposed Solution

1. **Add a "Metadata & Filtering" section** to the Kernel Memory integration doc (`docs/kernel-memory-integration.md`)
2. **Create a dedicated sample** (`src/src/Samples/FilteredSearch`) showing:
   - Importing documents with tags (category, source, department, etc.)
   - Searching with `MemoryFilter` to restrict results by tag
   - Combining semantic relevance with metadata filtering
   - Multi-tenant scenario: each tenant's docs tagged with `tenantId`

### Example Code for Docs

```csharp
// Import with tags
await memory.ImportTextAsync(
    "Our return policy allows returns within 30 days.",
    documentId: "faq-returns",
    tags: new TagCollection
    {
        { "department", "support" },
        { "topic", "returns" }
    });

// Search with filter — only "support" department docs
var filter = new MemoryFilter().ByTag("department", "support");
var results = await memory.SearchAsync("refund policy", filter: filter);
```

### Expected Impact

- Unlocks a key Kernel Memory feature that's invisible without docs
- Enables multi-tenant RAG architectures
- Low effort, high documentation value

---

## Scenario 6: ONNX Runtime GenAI Integration for Full Local RAG

**Priority:** 🟡 Medium
**Effort:** High (5–8 days)
**Origin:** GEMINI-3

### Problem

The current library covers the **embedding** side of RAG but relies on Ollama or external LLMs for text generation. Many developers want a **100% local, offline RAG pipeline** without installing separate services. `Microsoft.ML.OnnxRuntimeGenAI` provides local text generation using the same ONNX stack, making it a natural companion.

### Proposed Solution

Create a sample (and optional companion package) that pairs local embeddings with ONNX Runtime GenAI for fully offline RAG:

1. **Sample: `src/src/Samples/RagLocalGenAI`**
   - Use `ElBruno.LocalEmbeddings` for document vectorization and retrieval
   - Use `Microsoft.ML.OnnxRuntimeGenAI` (with a small model like Phi-3-mini ONNX) for answer generation
   - Wire both through `Microsoft.Extensions.AI` abstractions (`IEmbeddingGenerator` + `IChatClient`)
   - No network calls, no Ollama, no Docker — pure .NET

2. **Optional: `ElBruno.LocalEmbeddings.GenAI` companion package**
   - Builder extension: `new KernelMemoryBuilder().WithLocalEmbeddings().WithLocalTextGeneration(genAiOptions).Build()`
   - Uses `Microsoft.Extensions.AI.IChatClient` interface for text generation

### Example Architecture

```
User Query
    │
    ▼
┌──────────────────────────┐
│ ElBruno.LocalEmbeddings  │ ← vectorize query
│ (IEmbeddingGenerator)    │
└──────────┬───────────────┘
           ▼
┌──────────────────────────┐
│ Vector Store (in-memory) │ ← retrieve top-K docs
└──────────┬───────────────┘
           ▼
┌──────────────────────────────┐
│ Microsoft.ML.OnnxRuntimeGenAI│ ← generate answer from context
│ (IChatClient)                │
└──────────────────────────────┘
           ▼
      Answer + Citations
```

### Design Considerations

- Model download size is significant (~2–4 GB for Phi-3-mini ONNX); document clearly
- Use `Microsoft.Extensions.AI.IChatClient` abstraction so the text generator is swappable
- Consider GPU vs CPU inference options and document system requirements
- This positions the library as a complete local AI toolkit, not just an embeddings utility

### Expected Impact

- Enables fully offline RAG scenarios (air-gapped environments, edge devices, privacy-sensitive workloads)
- Leverages existing Microsoft.Extensions.AI abstractions for both components
- Strong differentiation vs. cloud-only embedding libraries

---

## Scenario 7: Microsoft.Extensions.AI Middleware Pipeline Sample

**Priority:** 🟢 Low
**Effort:** Low (1–2 days)
**Origin:** GEMINI-4

### Problem

`Microsoft.Extensions.AI` provides a middleware/pipeline pattern for wrapping AI clients with cross-cutting concerns (caching, logging, telemetry, rate limiting). Since `LocalEmbeddingGenerator` implements `IEmbeddingGenerator<string, Embedding<float>>`, it can participate in these pipelines — but no sample demonstrates this.

### Proposed Solution

Create a sample (`src/src/Samples/MiddlewarePipeline`) that shows:

1. **Distributed caching** — Cache embeddings for repeated texts using `IDistributedCache`
2. **Logging** — Log embedding generation calls with `ILogger` via the M.E.AI logging middleware
3. **OpenTelemetry** — Export embedding generation metrics (latency, dimensions, batch size)
4. **Pipeline composition** — Chain multiple middleware layers

### Example Code

```csharp
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;

var services = new ServiceCollection();

services.AddLocalEmbeddings();
services.AddDistributedMemoryCache();
services.AddLogging(b => b.AddConsole());

// Build pipeline: Generator → Cache → Logging → OpenTelemetry
services.AddEmbeddingGenerator(pipeline => pipeline
    .UseDistributedCache()
    .UseLogging()
    .UseOpenTelemetry()
    .Use(sp => sp.GetRequiredService<LocalEmbeddingGenerator>()));
```

### Design Considerations

- Depends on `Microsoft.Extensions.AI` pipeline APIs being stable
- The caching middleware is especially valuable — embedding the same text twice should be instant
- OpenTelemetry integration enables production monitoring dashboards

### Expected Impact

- Shows the library as a first-class citizen in the M.E.AI ecosystem
- Demonstrates production patterns (caching, observability) that go beyond "hello world"
- Very low effort since the middleware already exists — just needs a sample

---

## Scenario 8: Multi-Model Comparison Sample

**Priority:** 🟢 Low
**Effort:** Low (1–2 days)
**Origin:** GEMINI-5

### Problem

The library supports multiple HuggingFace models (all-MiniLM-L6-v2, all-MiniLM-L12-v2, etc.) but no sample shows the quality/performance tradeoffs between them. Developers don't know which model to choose for their use case.

### Proposed Solution

Create a sample (`src/src/Samples/ModelComparison`) that:

1. Loads 2–3 models side by side
2. Runs the same set of queries against the same corpus
3. Compares:
   - **Quality**: Which model ranks the expected document highest?
   - **Speed**: Embeddings/second and latency per model
   - **Dimensions**: 384 vs 768 vector size tradeoffs (memory, storage)
4. Outputs a comparison table

### Example Output

```
Model                         Dims  Avg Latency  Precision@3  Size
all-MiniLM-L6-v2              384   12ms/text    85%          22MB
all-MiniLM-L12-v2             384   18ms/text    89%          33MB
bge-small-en-v1.5             384   15ms/text    87%          33MB
```

### Design Considerations

- Use `BenchmarkDotNet` or simple `Stopwatch` for timing (BenchmarkDotNet for a published blog post, Stopwatch for a simple sample)
- Include guidance on when to pick each model (latency-sensitive vs. quality-sensitive)
- Could integrate with Scenario 4 (Evaluation) for automated quality scoring

### Expected Impact

- Removes guesswork from model selection
- Demonstrates the library's multi-model flexibility
- Useful as blog post / conference talk material

---

## Summary Table

| # | Scenario | Priority | Effort | Package Impact |
|---|----------|----------|--------|---------------|
| 1 | Built-in InMemoryVectorStore | 🔴 High | Medium | `ElBruno.LocalEmbeddings.VectorData` |
| 2 | FindClosest Convenience API | 🔴 High | Low | `ElBruno.LocalEmbeddings` |
| 3 | GenerateAndUpsert Pipeline Helper | 🟡 Medium | Low | `ElBruno.LocalEmbeddings.VectorData` |
| 4 | M.E.AI Evaluation Integration | 🟡 Medium | Medium | New sample + optional helper |
| 5 | TagCollection & Filtering Docs | 🟡 Medium | Low | Docs + sample only |
| 6 | ONNX Runtime GenAI Local RAG | 🟡 Medium | High | New sample + optional package |
| 7 | M.E.AI Middleware Pipeline Sample | 🟢 Low | Low | Sample only |
| 8 | Multi-Model Comparison Sample | 🟢 Low | Low | Sample only |

### Recommended Implementation Order

1. **Scenario 2** (FindClosest) — Quick win, high impact, low effort
2. **Scenario 1** (InMemoryVectorStore) — Unblocks the entire VectorData story
3. **Scenario 5** (TagCollection docs) — Low-effort documentation improvement
4. **Scenario 3** (GenerateAndUpsert) — Natural follow-up to Scenario 1
5. **Scenario 7** (Middleware pipeline) — Quick sample, shows M.E.AI depth
6. **Scenario 8** (Multi-model comparison) — Useful reference material
7. **Scenario 4** (Evaluation) — Medium effort, aligns with M.E.AI growth
8. **Scenario 6** (ONNX GenAI) — Highest effort, biggest long-term payoff

