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
