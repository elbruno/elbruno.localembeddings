# Project Context

- **Owner:** Bruno Capuano
- **Project:** LocalEmbeddings — a .NET library for local embedding generation using Microsoft.Extensions.AI abstractions
- **Stack:** .NET 10, C#, Microsoft.Extensions.AI, ONNX Runtime, HuggingFace models (all-MiniLM)
- **Created:** 2026-02-12

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-02-28: Harrier Documentation Update

**Completed:** All 6 documentation items identified in the architecture review have been updated.

**What was done:**
1. **README.md** — Added Harrier to Features section with 🦅 icon, Installation section, Quick Start (example #5), Documentation table, and Samples table
2. **docs/changelog.md** — Added comprehensive [Unreleased] section for Harrier v1.0.0 with added/changed/fixed entries covering the entire audit phase
3. **docs/harrier-integration.md** — Added "## Migrating from MiniLM to Harrier" migration guide covering vector store re-indexing (384→640 dims), DI swap, instruction prefix setup, model size considerations, and MaxSequenceLength optimization
4. **samples/README.md** — Added HarrierConsoleApp entry after ConsoleApp, DocumentRagFoundry entry after RagFoundryLocal, VisionMemoryAgentSample and NpuBenchmarkSample entries in the Image Samples section
5. **docs/dependency-injection.md** — Added "## Multi-Model Scenarios: DI Registration Conflicts" section documenting TryAddSingleton behavior, the first-registration-wins pattern, and three solutions (keyed services, explicit creation, wrapper service). Also added "## Harrier Integration" subsection with all 4 overloads and migration warning
6. **src/ElBruno.LocalEmbeddings.Harrier/README.md** — Created new package-specific README with quick start, model details, features list, configuration, and DI registration examples. Updated Harrier.csproj to use local README.md instead of root

**Verification:** Solution builds successfully (`dotnet build`). All changes follow established conventions: markdown formatting, XML documentation style, DI patterns, and branding (ElBruno prefix).

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

### 2026-02-28: Harrier Architecture Review

**Scope:** Full repository review with focus on `ElBruno.LocalEmbeddings.Harrier` package.

**Architecture:**
- Harrier follows all established patterns: `IEmbeddingGenerator` interface, Options pattern, DI extensions, `CreateAsync()` factory, security guards (SEC-001/002/006), perf patterns (ArrayPool, SIMD, SessionOptions disposal)
- 6 public types: `HarrierEmbeddingGenerator`, `HarrierEmbeddingsOptions`, `HarrierModelVariant`, `HarrierModelDownloader`, `HarrierOnnxEmbeddingModel`, `HarrierTokenizer`
- DI: `AddHarrierEmbeddings()` with 3 overloads (Action, options, IConfiguration). Missing `string modelName` convenience overload.
- No `IHarrierModelDownloader` interface (unlike base library's `IModelDownloader`)
- Harrier csproj has only ProjectReference to base — no explicit refs for OnnxRuntime/Tokenizers used directly

**Documentation gaps found:**
- README.md has zero mention of Harrier (or ImageEmbeddings)
- Changelog not updated for Harrier
- samples/README.md missing HarrierConsoleApp, DocumentRagFoundry, VisionMemoryAgentSample
- No MiniLM → Harrier migration guide

**Solution structure issues:**
- DocumentRagFoundry sample missing from slnx
- NPU directories (`src/ElBruno.LocalEmbeddings.Npu*/`) are empty stubs with only bin/obj — no csproj or source files

**Report written to:** `.squad/decisions/inbox/ripley-harrier-arch-review.md`
