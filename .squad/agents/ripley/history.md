# Project Context

- **Owner:** Bruno Capuano
- **Project:** LocalEmbeddings — a .NET library for local embedding generation using Microsoft.Extensions.AI abstractions
- **Stack:** .NET 10, C#, Microsoft.Extensions.AI, ONNX Runtime, HuggingFace models (all-MiniLM)
- **Created:** 2026-02-12

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-02-12: Solution Structure Established

**Architecture:**
- `src/LocalEmbeddings/` — Main library implementing `IEmbeddingGenerator<string, Embedding<float>>`
- `tests/LocalEmbeddings.Tests/` — xUnit test project
- `samples/ConsoleApp/` — Console sample app

**Key Types:**
- `LocalEmbeddingGenerator` — Main entry point, implements M.E.AI interface
- `OnnxEmbeddingModel` — Internal ONNX inference wrapper
- `ModelDownloader` — HuggingFace model fetching/caching
- `LocalEmbeddingsOptions` — Configuration via Options pattern
- `ServiceCollectionExtensions.AddLocalEmbeddings()` — DI registration

**Packages (latest versions as of setup):**
- Microsoft.Extensions.AI.Abstractions 10.3.0
- Microsoft.ML.OnnxRuntime 1.24.1
- Microsoft.ML.Tokenizers 2.0.0
- Microsoft.Extensions.DependencyInjection.Abstractions 10.0.3

**Conventions:**
- XML documentation enabled with `GenerateDocumentationFile`
- `TreatWarningsAsErrors` globally via Directory.Build.props
- File-scoped namespaces preferred (see .editorconfig)

### 2026-02-28: Cross-Project Tracking Issue Created

**Issue:** #38 — "Apply security, performance & CI lessons to related ElBruno projects"  
**URL:** https://github.com/elbruno/elbruno.localembeddings/issues/38

After completing the v1.1.0 security & performance audit, consolidated 9 security findings, 17 performance findings, and 3 CI/Linux lessons into a reusable checklist. Created a comprehensive issue with actionable items for applying these lessons to 9 related ElBruno projects:
- ElBruno.HuggingFace.Downloader, ElBruno.VibeVoiceTTS, ElBruno.QwenTTS, ElBruno.Text2Image, ElBruno.PersonaPlex
- ElBruno.Realtime, ElBruno.Connectors.SqliteVec, ElBruno.AgentsOrchestration, ElBruno AI Evaluation

**Checklists included:**
- Security (9 items: model integrity, path traversal, file validation, etc.)
- Performance (11 items: TensorPrimitives, ArrayPool, SessionOptions disposal, etc.)
- CI/Linux (3 critical patterns: SkippableFact, cross-platform file validation, git tag format)
- Squad rule: use gpt-5-mini for simple tasks

**Decision:** Recorded lessons in `.squad/decisions.md` under "Security Audit Findings" and "Performance Audit Findings" sections; CI lessons in `.squad/skills/ci-linux-test-failures/SKILL.md`.

### 2026-02-28: Improvement Roadmap Created

**Document:** `docs/roadmap.md`  
**Based on:** .NET community trends analysis (April 2026)

Analyzed current library capabilities against .NET AI ecosystem trends and created comprehensive roadmap with 5 priority tiers:

**Priority 1 — Core Library Improvements:**
- Batch embedding API with progress reporting
- Streaming embeddings API (`IAsyncEnumerable`)
- Embedding dimension reduction (PCA/truncation for edge)
- Embedding cache/persistence layer (LRU + optional SQLite)
- Multi-model embedding comparison tool

**Priority 2 — New Features:**
- Native AOT support for edge/serverless
- Hybrid search (vector + BM25) in InMemoryVectorStore
- Model Context Protocol (MCP) integration
- SLM integration sample (Phi-3 ONNX)
- Multi-modal embedding abstraction (text + image in shared space)

**Priority 3 — New Sample Scenarios:**
- Microsoft Agent Framework multi-agent sample
- Blazor WebAssembly edge RAG
- Real-time streaming agent UI (AG-UI Protocol)
- Semantic Memory + persistent vector store
- ARM64/Raspberry Pi 5 optimized sample

**Priority 4 — Ecosystem Integration:**
- M.E.AI middleware support (caching, telemetry, retry)
- VectorData 10.1.0 embedding generation support
- Semantic Kernel v2 memory connector
- Azure AI Foundry local agent integration

**Priority 5 — Performance & Edge:**
- ONNX Runtime 1.24.4 + FP16 precision
- Quantized model auto-selection
- Batch size auto-tuning
- NPU fallback telemetry
- WebAssembly deployment guide

**Key Insights:**
- Library already has strong foundation: core embeddings, image CLIP, NPU support, VectorData/KernelMemory integration
- Major gaps vs community: streaming APIs, hybrid search, Native AOT, MCP integration, persistent vector stores
- Emerging .NET AI ecosystem: M.E.AI 10.4.1 middleware, VectorData 10.1.0 hybrid search, Agent Framework, MCP, AG-UI
- Edge deployment is critical: Native AOT, WASM, Raspberry Pi, dimension reduction, quantization

**New Team Member Recommendations:**
1. **Edge/IoT Specialist** — ARM64, WASM, Native AOT, quantization expertise
2. **AI Framework Specialist** — Agent Framework, Semantic Kernel, MCP, multi-agent orchestration
3. **Data/Search Engineer** — Hybrid search, BM25, vector databases, embedding evaluation

**File Paths:**
- Roadmap: `docs/roadmap.md`
- Current packages: ElBruno.LocalEmbeddings (core), .ImageEmbeddings, .VectorData, .KernelMemory, .Npu, .Npu.Intel, .Npu.Qualcomm
