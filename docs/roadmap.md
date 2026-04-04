# ElBruno.LocalEmbeddings — Improvement Roadmap

> Generated 2026-02-28 — Based on .NET community analysis and ecosystem trends

## Current State Summary

ElBruno.LocalEmbeddings is a mature local embeddings library for .NET that provides:
- **Core text embeddings** via ONNX Runtime implementing Microsoft.Extensions.AI `IEmbeddingGenerator<string, Embedding<float>>`
- **Image embeddings** with CLIP model support (separate package)
- **NPU acceleration** via DirectML, Intel OpenVINO, and Qualcomm QNN execution providers
- **VectorData integration** with built-in in-memory vector store
- **Kernel Memory integration** via `ITextEmbeddingGenerator` adapter
- **HuggingFace model support** with automatic download and caching
- **Sample scenarios** including RAG applications, edge deployment (Raspberry Pi), and NPU benchmarks

The library has undergone comprehensive security (9 findings resolved) and performance (17 findings resolved) audits, and follows Microsoft.Extensions patterns consistently.

---

## Priority 1: Core Library Improvements

### 1.1 Batch Embedding API with Progress Reporting

**What:** Add `GenerateAsync(IEnumerable<string>, IProgress<EmbeddingProgress>)` overload with structured progress reporting for large document sets.

**Why:** Community demand for processing large document corpora (RAG indexing workflows). Current batch API doesn't expose progress, making long-running operations opaque. Similar to `InMemoryVectorStore.AddDocumentsAsync` progress pattern.

**Effort:** S  
**Dependencies:** None

### 1.2 Streaming Embeddings API

**What:** Add `IAsyncEnumerable<GeneratedEmbedding<Embedding<float>>>` return type for streaming embeddings as they are generated.

```csharp
await foreach (var item in generator.GenerateStreamingAsync(documents))
{
    await vectorStore.UpsertAsync(item.Value.Vector);
}
```

**Why:** Community interest in real-time embedding scenarios (e.g., live document ingestion). Aligns with .NET streaming patterns and Microsoft.Extensions.AI middleware model.

**Effort:** M  
**Dependencies:** None

### 1.3 Embedding Dimension Reduction Support

**What:** Add `DimensionReductionMethod` option (PCA, truncation) to reduce embedding dimensions from 384 → 256 or 128 for memory-constrained scenarios.

**Why:** Edge AI demand signal — Raspberry Pi, IoT devices need smaller vectors. Community asks for quantization beyond model-level int8.

**Effort:** L  
**Dependencies:** Requires mathematical validation; potentially new ONNX graph manipulation or post-processing

### 1.4 Embedding Cache/Persistence Layer

**What:** Add optional in-memory LRU cache for embeddings keyed by `(modelName, text, hash)` to avoid redundant inference. Persistent cache via SQLite optional.

```csharp
options.EnableEmbeddingCache = true;
options.CacheMaxSize = 10_000;
options.CachePersistence = CachePersistenceMode.InMemory; // or SQLite
```

**Why:** RAG applications re-embed the same queries repeatedly. Community demand for caching strategies to reduce latency and compute.

**Effort:** M  
**Dependencies:** SQLite persistence requires new dependency decision

### 1.5 Multi-Model Embedding Comparison Tool

**What:** Add `EmbeddingComparer` utility that takes multiple `IEmbeddingGenerator` instances and computes cross-model similarity matrices for evaluation.

```csharp
var comparer = new EmbeddingComparer([miniLM, bgeBase, e5Large]);
var report = await comparer.CompareAsync(queryPairs);
// Report shows which model best separates similar/dissimilar pairs
```

**Why:** Developers want to choose the right model for their domain. Community interest in embedding evaluation tools.

**Effort:** M  
**Dependencies:** None (uses existing APIs)

---

## Priority 2: New Features

### 2.1 Native AOT Support

**What:** Ensure the core library is fully Native AOT compatible — eliminate reflection, dynamic code generation, and ensure trimming annotations are correct.

**Why:** Native AOT is standard in .NET 10. Critical for edge AI (instant startup, reduced memory) and serverless scenarios. Community demand for trimmed, AOT-ready libraries.

**Effort:** M  
**Dependencies:** Requires ONNX Runtime Native AOT compatibility validation; may need workarounds for ORT dynamic loading

### 2.2 Hybrid Search Support (Vector + Keyword)

**What:** Add hybrid search to `InMemoryVectorStore` — combine vector similarity with BM25 keyword scoring. Expose via `SearchAsync` with `HybridSearchOptions`.

```csharp
var results = await collection.SearchAsync(
    vector: queryEmbedding,
    options: new() { 
        HybridMode = HybridSearchMode.WeightedFusion,
        KeywordWeight = 0.3f,
        VectorWeight = 0.7f
    });
```

**Why:** **Microsoft.Extensions.VectorData 10.1.0** now has hybrid search support. Community consensus: hybrid retrieval outperforms pure vector search for many RAG scenarios.

**Effort:** L  
**Dependencies:** Requires BM25 implementation or dependency (e.g., Lucene.Net.Analysis.Common)

### 2.3 Model Context Protocol (MCP) Integration

**What:** Enhance API surface for MCP server integration and coordinate with **ElBruno.ModelContextProtocol** library.

**Context:**
- Bruno owns **ElBruno.ModelContextProtocol** (NuGet: `ElBruno.ModelContextProtocol.MCPToolRouter` v0.1.0)
- Currently provides: semantic tool routing using `LocalEmbeddings`, `ToolRouter`/`ToolIndex` for semantic tool search, cosine similarity over embeddings
- Does NOT yet expose: MCP server endpoints/tools like `embed_text`, `search_embeddings`, `get_embedding_model_status`

**Work in THIS repo:**
- Ensure API is clean for MCP server integration
- Add convenience methods for MCP scenarios if needed
- Integration testing with ElBruno.ModelContextProtocol

**Work in ElBruno.ModelContextProtocol:**
- Add MCP server tools for embedding generation (tracked via GitHub issue on that repo)

**Why:** **Model Context Protocol** is becoming the standard for composable AI skills. ElBruno.ModelContextProtocol already uses LocalEmbeddings for semantic routing; extending it to expose embeddings as MCP tools enables broader agent interoperability.

**Effort:** S (for LocalEmbeddings API review)  
**Dependencies:** Coordination with ElBruno.ModelContextProtocol repo

### 2.4 Small Language Model (SLM) Integration Sample

**What:** Create sample combining **ElBruno.LocalLLMs** + **ElBruno.LocalEmbeddings** for RAG with zero cloud dependencies.

**Context:**
- Bruno owns **ElBruno.LocalLLMs** (NuGet: `ElBruno.LocalLLMs` v0.9.0 + `ElBruno.LocalLLMs.Rag` v0.1.0)
- `LocalChatClient` implements `IChatClient` via ONNX Runtime GenAI
- Supports: Phi-3.5 mini, Phi-4, Llama 3.x, Qwen2.5, Mistral, Gemma, DeepSeek-R1
- CPU/CUDA/DirectML execution providers, streaming, tool calls
- **LocalLLMs.Rag** package provides `IRagPipeline`, `LocalRagPipeline` that already supports `IEmbeddingGenerator<string, Embedding<float>>`

**Sample Goals:**
- Zero-cloud RAG stack: ElBruno.LocalEmbeddings → ElBruno.LocalLLMs → fully offline AI
- Show DI registration pattern combining both libraries
- Demonstrate RAG pipeline with local embeddings + local LLM generation

**Why:** Community trend toward **edge SLMs** running via ONNX. ElBruno.LocalLLMs.Rag already integrates with IEmbeddingGenerator, showcasing the full ElBruno "zero-cloud" stack.

**Effort:** S  
**Dependencies:** ElBruno.LocalLLMs packages (already published)

### 2.5 Multi-Modal Embedding Abstraction

**What:** Create unified abstraction for text + image embeddings in the same vector space. Extend `IEmbeddingGenerator` to support `IEmbeddingGenerator<TInput, Embedding<float>>` where `TInput` is union type.

```csharp
// Text and images in same 512-dim space
var textEmb = await multiModalGen.GenerateAsync("a sunset");
var imageEmb = await multiModalGen.GenerateAsync(imageStream);
var similarity = textEmb.CosineSimilarity(imageEmb);
```

**Why:** Community demand for **multi-modal embeddings** (text + image in shared space). CLIP already provides this, but current API is CLIP-specific, not abstracted.

**Effort:** L  
**Dependencies:** Requires rethinking `ImageEmbeddings` package API surface; potential breaking change to separate package

---

## Priority 3: New Sample Scenarios

### 3.1 Microsoft Agent Framework Multi-Agent Sample

**What:** Sample using **Microsoft Agent Framework** with local embeddings for document retrieval agent + summarization agent orchestration.

**Implementation:**
- Use **ElBruno.LocalEmbeddings** for semantic document retrieval
- Use **ElBruno.LocalLLMs** (`LocalChatClient`) as the local chat client for agent responses
- Multi-agent handoff: retrieve → summarize → answer

**Why:** **Microsoft Agent Framework** is the .NET standard for multi-agent orchestration. Showcases local embeddings + local LLM in agent workflows — fully offline, no cloud dependencies.

**Effort:** M  
**Dependencies:** Microsoft.Extensions.AI.Agents, ElBruno.LocalLLMs

### 3.2 Blazor WebAssembly Edge RAG

**What:** Blazor WASM sample running local embeddings + in-memory vector store entirely in browser (no server).

**Why:** Community interest in **edge AI** and WebAssembly deployment. Demonstrates ultimate edge scenario: embeddings running in browser.

**Effort:** L  
**Dependencies:** Requires ONNX Runtime WebAssembly support validation; WASM file size optimization

### 3.3 Real-Time Streaming Agent UI (AG-UI Protocol)

**What:** Sample using **AG-UI Protocol** for real-time streaming agent interactions with local embeddings powering semantic search in UI.

**Why:** AG-UI is emerging as standard for interactive agent UIs in Blazor/MAUI. Showcases local embeddings in streaming, real-time scenarios.

**Effort:** M  
**Dependencies:** Requires AG-UI NuGet packages

### 3.4 Persistent Vector Store Sample

**What:** Sample using persistent vector store (e.g., SQLite-Vec, Qdrant) with local embeddings for long-term context across sessions.

**Why:** Community demand for persistent vector stores beyond in-memory. Demonstrates RAG with durable storage — embeddings survive application restarts.

**Effort:** M  
**Dependencies:** Requires persistent vector store (Qdrant, SQLite-Vec, or custom)

### 3.5 Zero-Cloud RAG with ElBruno Stack

**What:** Sample combining **ElBruno.LocalEmbeddings** + **ElBruno.LocalLLMs** + **ElBruno.LocalEmbeddings.VectorData** for a complete offline RAG pipeline.

**Components:**
- Local embeddings via all-MiniLM (or similar) for document vectorization
- In-memory vector store from ElBruno.LocalEmbeddings.VectorData
- Local LLM generation via Phi-4 or Llama 3.x using ElBruno.LocalLLMs
- Full DI registration pattern showing integration of all three libraries

**Why:** Showcases the full ElBruno ecosystem for zero-cloud AI: no internet required, no cloud APIs, fully offline RAG stack. Target models: all-MiniLM (embeddings) + Phi-4 (generation).

**Effort:** S  
**Dependencies:** ElBruno.LocalLLMs, ElBruno.LocalEmbeddings.VectorData

### 3.6 MCP Tool Router Sample

**What:** Sample demonstrating **ElBruno.ModelContextProtocol.MCPToolRouter** with LocalEmbeddings for semantic tool discovery and routing.

**Implementation:**
- Index MCP tools using LocalEmbeddings
- Natural language tool queries → semantic routing to the right tool
- Show `ToolRouter`, `ToolIndex` integration with AI agents

**Why:** ElBruno.ModelContextProtocol already depends on LocalEmbeddings for semantic routing. Sample showcases how AI agents can discover and invoke the right tools using natural language.

**Effort:** S  
**Dependencies:** ElBruno.ModelContextProtocol.MCPToolRouter

### 3.7 ARM64 / Raspberry Pi 5 Optimized Sample

**What:** Sample optimized for **Raspberry Pi 5 / ARM64** with quantized models, reduced batch sizes, and telemetry for edge performance analysis.

**Why:** **Edge AI on IoT** is a major trend. Existing `RaspberryPiTiny` sample, but no dedicated optimization guide or benchmark data.

**Effort:** S  
**Dependencies:** None (builds on existing RaspberryPiTiny)

---

## Priority 4: Ecosystem Integration

### 4.1 Microsoft.Extensions.AI Middleware Support

**What:** Add built-in middleware for caching, telemetry, and retry logic using Microsoft.Extensions.AI middleware patterns.

```csharp
services.AddLocalEmbeddings(options => ...)
    .UseCaching()
    .UseOpenTelemetry()
    .UseRetry();
```

**Why:** **Microsoft.Extensions.AI 10.4.1** has standardized middleware patterns (`IEmbeddingGenerator` middleware). Community expects first-class middleware support.

**Effort:** M  
**Dependencies:** Requires Microsoft.Extensions.AI.Abstractions update

### 4.2 Microsoft.Extensions.VectorData Embedding Generation Support

**What:** Implement `IEmbeddingGenerator` integration with VectorData's built-in embedding generation support (new in 10.1.0).

```csharp
services.AddVectorStore<InMemoryVectorStore>()
    .WithEmbeddingGeneration<LocalEmbeddingGenerator>();
```

**Why:** **Microsoft.Extensions.VectorData 10.1.0** now supports automatic embedding generation on insert. Current VectorData package doesn't leverage this.

**Effort:** M  
**Dependencies:** Requires VectorData package upgrade and API surface changes

### 4.3 Azure AI Foundry Local Agent Integration (Enhanced)

**What:** Enhanced sample showing Azure AI Foundry local agent with local embeddings (no Foundry-hosted embeddings). Agent-focused version of existing `RagFoundryLocal`.

**Why:** Developers want to use Foundry orchestration but keep embeddings local (data privacy, cost). Current `RagFoundryLocal` sample exists; enhance with agent-specific scenarios.

**Effort:** S  
**Dependencies:** Builds on existing RagFoundryLocal



---

## Priority 5: Performance & Edge

### 5.1 ONNX Runtime 1.24.4 Upgrade + FP16 Precision

**What:** Upgrade to **ONNX Runtime 1.24.4** and add `Precision` option (FP32, FP16) for models that support half-precision inference.

**Why:** ORT 1.24.4 adds context binary caching for QNN EP and FP16 precision support. Community demand for faster inference on NPUs.

**Effort:** S  
**Dependencies:** Requires validating FP16 model availability for sentence-transformers

### 5.2 Quantized Model Auto-Selection

**What:** Enhance `PreferQuantized` option to auto-detect and download quantized models (int8, uint8, qint8) from HuggingFace when available.

**Why:** Community asks for "just work" quantization. Current `PreferQuantized` requires manual model export.

**Effort:** M  
**Dependencies:** Requires HuggingFace model metadata parsing (model card, config.json)

### 5.3 Batch Size Auto-Tuning

**What:** Add adaptive batch size logic that profiles initial inference latency and adjusts batch size to optimize throughput vs memory.

```csharp
options.BatchSizeMode = BatchSizeMode.Auto; // Profiles and adjusts
```

**Why:** Edge devices have varying memory constraints. Auto-tuning maximizes throughput without OOM.

**Effort:** M  
**Dependencies:** Requires memory profiling and benchmarking infrastructure

### 5.4 NPU Fallback Telemetry

**What:** Add OpenTelemetry spans and metrics for NPU fallback events (e.g., QNN → CPU fallback due to unsupported op).

**Why:** Developers need visibility into why NPU acceleration isn't working. Current `FallbackReason` property is insufficient for debugging.

**Effort:** S  
**Dependencies:** Requires OpenTelemetry dependency

### 5.5 WebAssembly / Browser Deployment Guide

**What:** Create detailed guide + sample for deploying local embeddings in Blazor WASM, including WASM file size optimization and ONNX Runtime WASM setup.

**Why:** Community interest in **browser-side AI**. WebAssembly is a key edge deployment target.

**Effort:** M  
**Dependencies:** Linked to Priority 3.2 (Blazor WASM sample)

---

## New Team Members Recommendation

Based on roadmap scope and current team composition (Lead/Architect, Core Dev, Integration, Security, Performance, Tester), **recommend adding**:

### 1. **Edge/IoT Specialist** (new role)
**Expertise:** ARM64 optimization, WebAssembly, Native AOT, quantization, edge deployment patterns  
**Justification:** Significant roadmap items focus on edge scenarios (Native AOT, Raspberry Pi, WASM, dimension reduction). Current team lacks dedicated edge expertise.  
**Priorities:** 1.3, 2.1, 3.2, 3.5, 5.2, 5.3, 5.5

### 2. **AI Framework Specialist** (new role)
**Expertise:** Microsoft Agent Framework, Model Context Protocol, AG-UI, multi-agent orchestration, ElBruno ecosystem integration  
**Justification:** Multiple ecosystem integration items require deep knowledge of emerging .NET AI frameworks and the ElBruno library ecosystem. Current Integration role is broad; need focused AI framework expertise.  
**Priorities:** 2.3, 2.4, 3.1, 3.3, 3.5, 3.6, 4.3

### 3. **Data/Search Engineer** (new role)
**Expertise:** Hybrid search (BM25), vector databases, search relevance, embedding evaluation, caching strategies  
**Justification:** Hybrid search, persistent vector stores, and embedding comparison tools require search/IR expertise beyond current team scope.  
**Priorities:** 1.4, 1.5, 2.2, 3.4

---

## Implementation Phasing Recommendation

**Phase 1 (Q2 2026):** Quick wins that align with M.E.AI/VectorData ecosystem updates  
→ 1.1, 1.2, 4.1, 4.2, 5.1

**Phase 2 (Q3 2026):** New features that differentiate the library  
→ 2.1, 2.2, 1.4, 1.5

**Phase 3 (Q4 2026):** Advanced scenarios and emerging protocols  
→ 2.3, 2.4, 3.1, 3.3, 3.5, 3.6

**Phase 4 (Q1 2027):** Edge optimization and long-tail samples  
→ 1.3, 3.2, 3.4, 3.7, 5.2, 5.3, 5.5

---

## Success Metrics

- **Adoption:** NuGet download growth (target: 2x by Q4 2026)
- **Ecosystem:** Number of community-contributed samples using the library with other .NET AI frameworks
- **Performance:** Embedding generation latency reduction on edge devices (target: 20% improvement via quantization + AOT)
- **Community Engagement:** GitHub stars, issues/PRs from external contributors
- **Feature Completeness:** Coverage of Microsoft.Extensions.AI/VectorData 10.x API surface

---

**Next Steps:**
1. Review roadmap with stakeholders (Bruno Capuano)
2. Prioritize Phase 1 items and assign to squad members
3. Create tracking issues for each roadmap item
4. Onboard new team members (Edge/IoT, AI Framework, Data/Search specialists)
5. Establish quarterly milestones and community engagement plan
