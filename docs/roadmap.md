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

**What:** Create `ElBruno.LocalEmbeddings.Mcp` package that exposes embeddings as MCP tools/resources for AI agent interoperability.

```csharp
// Expose as MCP resource
services.AddMcpServer()
    .AddLocalEmbeddingsTool(); // Agents can request embeddings via MCP
```

**Why:** **Model Context Protocol** is becoming the standard for composable AI skills. Allows local embeddings to be used by any MCP-compatible agent framework.

**Effort:** M  
**Dependencies:** Requires MCP NuGet packages; new companion package

### 2.4 Small Language Model (SLM) Integration Sample

**What:** Create sample showing local SLM (Phi-3 via ONNX) + local embeddings for classification/ranking tasks without cloud LLM.

**Why:** Community trend toward **edge SLMs** (Phi-3, Llama 3) running via ONNX. Showcases "zero-cloud" AI stack: local embeddings + local LLM.

**Effort:** M  
**Dependencies:** Requires Phi-3 ONNX model and Microsoft.ML.OnnxRuntimeGenAI integration

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

**Why:** **Microsoft Agent Framework** is the .NET standard for multi-agent orchestration. Showcases local embeddings in agent handoff workflows (retrieve → summarize → answer).

**Effort:** M  
**Dependencies:** Requires Microsoft.Extensions.AI.Agents (or Semantic Kernel multi-agent APIs)

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

### 3.4 Semantic Memory + Persistent Vector Store

**What:** Sample using **Semantic Kernel Semantic Memory** with persistent vector store (e.g., SQLite-Vec) for long-term context across sessions.

**Why:** **Semantic Memory** is standard for persistent context in multi-turn LLM interactions. Community demand for persistent vector stores beyond in-memory.

**Effort:** M  
**Dependencies:** Requires persistent vector store (Qdrant, SQLite-Vec, or custom)

### 3.5 ARM64 / Raspberry Pi 5 Optimized Sample

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

### 4.3 Semantic Kernel v2 Memory Connector

**What:** Create native Semantic Kernel v2 memory connector for `InMemoryVectorStore` (or as SK Memory abstraction).

**Why:** Semantic Kernel is the standard .NET AI orchestration framework. Current KernelMemory integration is separate; SK v2 needs first-class support.

**Effort:** M  
**Dependencies:** Requires Semantic Kernel v2 Memory abstractions

### 4.4 Azure AI Foundry Local Agent Integration

**What:** Sample showing Azure AI Foundry local agent with local embeddings (no Foundry-hosted embeddings).

**Why:** Developers want to use Foundry orchestration but keep embeddings local (data privacy, cost). Existing `RagFoundryLocal` sample, but needs agent-focused version.

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
**Expertise:** Microsoft Agent Framework, Semantic Kernel, Model Context Protocol, AG-UI, multi-agent orchestration  
**Justification:** Multiple ecosystem integration items require deep knowledge of emerging .NET AI frameworks. Current Integration role is broad; need focused AI framework expertise.  
**Priorities:** 2.3, 2.4, 3.1, 3.3, 4.3, 4.4

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
→ 2.3, 2.4, 3.1, 3.3

**Phase 4 (Q1 2027):** Edge optimization and long-tail samples  
→ 1.3, 3.2, 3.5, 5.2, 5.3, 5.5

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
