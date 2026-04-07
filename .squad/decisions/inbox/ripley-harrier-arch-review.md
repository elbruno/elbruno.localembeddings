# Architecture & Documentation Review — Harrier Package Focus

**By:** Ripley (Lead / Architect)  
**Date:** 2026-02-28  
**Scope:** Full repository review with special focus on `ElBruno.LocalEmbeddings.Harrier`

---

## 1. Architecture Consistency

### ✅ Good — Harrier follows established patterns

- **File:** `src/ElBruno.LocalEmbeddings.Harrier/HarrierEmbeddingGenerator.cs`  
  Correctly implements `IEmbeddingGenerator<string, Embedding<float>>` and `IAsyncDisposable`. Class structure mirrors `LocalEmbeddingGenerator` exactly: private constructor, `CreateAsync()` factory, `GenerateAsync`, `CountTokens`, `GetService`, dispose pattern.

- **File:** `src/ElBruno.LocalEmbeddings.Harrier/Options/HarrierEmbeddingsOptions.cs`  
  Options class mirrors `LocalEmbeddingsOptions` faithfully. Adds model-specific settings (`ModelVariant`, `InstructionPrefix`) where appropriate. All properties have XML docs.

- **File:** `src/ElBruno.LocalEmbeddings.Harrier/Extensions/ServiceCollectionExtensions.cs`  
  DI registration follows the 3-overload pattern (`Action<T>`, options instance, `IConfiguration`). Uses `TryAddSingleton` to prevent double registration. Sync-over-async warning documented.

- **File:** `src/ElBruno.LocalEmbeddings.Harrier/HarrierOnnxEmbeddingModel.cs`  
  Uses `ArrayPool<long>.Shared` for flat tensor allocation (matching PERF-01 pattern). `SessionOptions` properly disposed with `using var` (matching PERF-03/15/16). `AsTensor<float>().ToArray()` path (matching PERF-06/07).

- **File:** `src/ElBruno.LocalEmbeddings.Harrier/HarrierModelDownloader.cs`  
  SHA-256 sidecar integrity pattern applied (matching SEC-001). Path traversal guard present (matching SEC-006). `SocketsHttpHandler` with `PooledConnectionLifetime` on parameterless constructor (matching SEC-002).

- **General:** File-scoped namespaces, nullable reference types, sealed classes, `ConfigureAwait(false)`, `CancellationToken` propagation — all consistent.

### ⚠️ Improvement Needed — DI overload gap

- **File:** `src/ElBruno.LocalEmbeddings.Harrier/Extensions/ServiceCollectionExtensions.cs`  
  **Finding:** Base library has 4 DI overloads: `Action<T>`, options instance, `string modelName`, `IConfiguration`. Harrier has only 3 — missing the convenience `AddHarrierEmbeddings(string modelName)` overload.  
  **Recommendation:** Add `AddHarrierEmbeddings(string modelName)` for parity.

### ⚠️ Improvement Needed — No IModelDownloader interface for Harrier

- **File:** `src/ElBruno.LocalEmbeddings.Harrier/HarrierModelDownloader.cs`  
  **Finding:** Base library defines `IModelDownloader` for testability and DI. Harrier's `HarrierModelDownloader` is a concrete sealed class with no interface.  
  **Recommendation:** Extract `IHarrierModelDownloader` interface for consistent testability. Not blocking, but reduces test isolation options.

### ⚠️ Improvement Needed — No IHttpClientFactory integration in Harrier DI

- **File:** `src/ElBruno.LocalEmbeddings.Harrier/Extensions/ServiceCollectionExtensions.cs:101-108`  
  **Finding:** Base library's `AddLocalEmbeddingsCore()` uses `services.AddHttpClient<IModelDownloader, ModelDownloader>()` for proper HttpClient lifecycle. Harrier's `AddHarrierEmbeddingsCore()` goes directly to `CreateAsync().GetAwaiter().GetResult()` using a static `HttpClient` without IHttpClientFactory.  
  **Recommendation:** When `IHarrierModelDownloader` is added, integrate with `IHttpClientFactory` for production HttpClient lifecycle.

### ⚠️ Improvement Needed — Static HttpClient without SocketsHttpHandler

- **File:** `src/ElBruno.LocalEmbeddings.Harrier/HarrierEmbeddingGenerator.cs:26`  
  **Finding:** `SharedModelDownloadHttpClient = new()` creates a bare HttpClient. This gets passed to `HarrierModelDownloader(HttpClient, options)` which doesn't add `SocketsHttpHandler`. The SEC-002 fix (PooledConnectionLifetime) only applies in the no-arg `HarrierModelDownloader(options)` path.  
  **Note:** Base library has the identical pattern — this is pre-existing. Both the base and Harrier static paths bypass the SEC-002 fix.  
  **Recommendation:** Apply `SocketsHttpHandler { PooledConnectionLifetime = ... }` to both static HttpClient fields.

### ⚠️ Improvement Needed — Public visibility of implementation types

- **Files:** `HarrierOnnxEmbeddingModel.cs`, `HarrierModelDownloader.cs`, `HarrierTokenizer.cs`  
  **Finding:** All three are `public sealed` but represent implementation internals. The established decision (2026-02-12) states "Internal types (`OnnxEmbeddingModel`, `ModelDownloader`) are not exposed." However, the base library also exposes these types publicly. This is a pre-existing API surface choice, not Harrier-specific.  
  **Recommendation:** Consider making these `internal` in a future major version for both packages. For now, document that `HarrierEmbeddingGenerator` is the only supported public entry point.

---

## 2. Documentation Gaps

### ❌ Issue Found — README.md missing packages

- **File:** `README.md`  
  **Finding:** The main README does not mention:
  - `ElBruno.LocalEmbeddings.Harrier` — no install command, no quick start, no feature mention
  - `ElBruno.LocalEmbeddings.ImageEmbeddings` — not in Features, Installation, or Documentation table
  - NPU packages (even as planned/coming-soon)
  
  **Recommendation:** Add Harrier and ImageEmbeddings to the README Features section, Installation section, and Documentation table. At minimum, add install commands and a 3-line quick start for Harrier.

### ❌ Issue Found — README documentation table incomplete

- **File:** `README.md:154-166`  
  **Finding:** The documentation table links to 10 docs but omits `docs/harrier-integration.md`. Also no ImageEmbeddings doc link.  
  **Recommendation:** Add `| [Harrier Integration](docs/harrier-integration.md) | Microsoft Harrier-OSS-v1 local embedding model |` to the table.

### ❌ Issue Found — README samples table incomplete

- **File:** `README.md:117-126`  
  **Finding:** The samples table lists 7 samples but the repository has 15+ sample projects. Missing: HarrierConsoleApp, DocumentRagFoundry, ConsoleAppLite, RaspberryPiTiny, BenchmarkSample, VisionMemoryAgentSample, NpuBenchmarkSample.  
  **Recommendation:** Update the table or reference `samples/README.md` more prominently.

### ⚠️ Improvement Needed — No migration guide

- **File:** `docs/harrier-integration.md`  
  **Finding:** Has an excellent comparison table (model type, dim, context, etc.) but no step-by-step migration guide for existing MiniLM users switching to Harrier. Key migration concerns (different dimensions break existing vector stores, instruction prefix requirement, model variant selection) are not called out as migration steps.  
  **Recommendation:** Add a "## Migration from MiniLM" section with: (1) vector store re-indexing warning, (2) DI swap instructions, (3) instruction prefix setup, (4) dimension change impact.

### ⚠️ Improvement Needed — Changelog not updated for Harrier

- **File:** `docs/changelog.md`  
  **Finding:** Last entry is `[Unreleased] - 2026-02-14`. No mention of Harrier, ImageEmbeddings, VectorData, or the security/performance audit work.  
  **Recommendation:** Add changelog entries for all recent packages and the security/performance audit phases.

### ⚠️ Improvement Needed — samples/README.md outdated

- **File:** `samples/README.md`  
  **Finding:** Lists 8 samples. Missing: HarrierConsoleApp, DocumentRagFoundry, VisionMemoryAgentSample, NpuBenchmarkSample.  
  **Recommendation:** Add entries for the 4 missing samples.

### ✅ Good — Harrier XML documentation

- **Files:** All `src/ElBruno.LocalEmbeddings.Harrier/*.cs`  
  All public types, methods, properties, and parameters have comprehensive XML docs with `<remarks>`, `<example>`, and `<exception>` tags where appropriate. Thread safety documented. Instruction prefix guidance included. Well done.

### ✅ Good — Dedicated Harrier docs page

- **File:** `docs/harrier-integration.md`  
  Thorough standalone guide covering installation, quick start, model details, variants, configuration, instruction prefixes, DI, appsettings.json, differences from base, token counting, API reference, and troubleshooting.

---

## 3. Public API Surface Review

### ✅ Good — Naming consistency

All Harrier types use the `Harrier` prefix consistently: `HarrierEmbeddingGenerator`, `HarrierEmbeddingsOptions`, `HarrierModelDownloader`, `HarrierOnnxEmbeddingModel`, `HarrierTokenizer`, `HarrierModelVariant`. DI method: `AddHarrierEmbeddings()`. Follows the established pattern.

### ✅ Good — Extension methods work with Harrier automatically

- **File:** `src/ElBruno.LocalEmbeddings/EmbeddingGeneratorExtensions.cs`  
  `GenerateAsync(string)`, `GenerateEmbeddingAsync(string)`, and `FindClosestAsync(...)` are extension methods on `IEmbeddingGenerator<string, Embedding<float>>`. They work with `HarrierEmbeddingGenerator` out of the box since it implements the same interface.

### ✅ Good — Instruction prefix API design

- **File:** `src/ElBruno.LocalEmbeddings.Harrier/Options/HarrierEmbeddingsOptions.cs:66-75`  
  `InstructionPrefix` is a nullable string property with a sensible default. Setting to null/empty disables it. Constants (`DefaultInstructionPrefix`) provided. Documented with common task-specific prefixes. This design is clean and extensible — users can pass any instruction without API changes.

### ⚠️ Improvement Needed — DI conflict not documented

- **Files:** `src/ElBruno.LocalEmbeddings/Extensions/ServiceCollectionExtensions.cs`, `src/ElBruno.LocalEmbeddings.Harrier/Extensions/ServiceCollectionExtensions.cs`  
  **Finding:** Both register `IEmbeddingGenerator<string, Embedding<float>>` via `TryAddSingleton`. Calling both `AddLocalEmbeddings()` and `AddHarrierEmbeddings()` silently resolves to whichever was registered first. This is correct `TryAdd` behavior, but consumers may be surprised.  
  **Recommendation:** Document this in both DI extension docs: "If you register both base and Harrier embeddings, only the first registration wins. Use keyed services or explicit singleton registration for multi-model scenarios."

### ⚠️ Improvement Needed — No shared base abstraction worth extracting

- **Finding:** Both generators share patterns (options, model download, tokenize, ONNX inference) but the implementations differ enough (encoder vs decoder, mean pooling vs last-token, vocab.txt vs tokenizer.json) that a shared abstract base would add complexity without reducing code. Current approach of independent implementations sharing the `IEmbeddingGenerator` interface is the right call.  
  **Recommendation:** No action needed. The interface-based design is correct.

---

## 4. Package Dependency Graph

### ✅ Good — Clean dependency tree

```
ElBruno.LocalEmbeddings.Harrier
  └── ElBruno.LocalEmbeddings (ProjectReference)
        ├── Microsoft.Extensions.AI.Abstractions 10.3.0
        ├── Microsoft.ML.OnnxRuntime 1.24.2
        ├── Microsoft.ML.Tokenizers 2.0.0
        ├── ElBruno.HuggingFace.Downloader 0.5.0
        ├── Microsoft.Extensions.DI.Abstractions 10.0.3
        ├── Microsoft.Extensions.Http 10.0.3
        ├── Microsoft.Extensions.Options 10.0.3
        ├── Microsoft.Extensions.Options.ConfigurationExtensions 10.0.3
        └── System.Numerics.Tensors 9.0.3
```

No circular dependencies. No unnecessary dependencies. Harrier's only direct dependency is the base library.

### ✅ Good — Version consistency

All Microsoft.Extensions packages at 10.0.3. OnnxRuntime at 1.24.2 across all projects. `ElBruno.HuggingFace.Downloader` at 0.5.0 in both locations.

### ⚠️ Improvement Needed — Harrier uses transitive types without direct references

- **File:** `src/ElBruno.LocalEmbeddings.Harrier/ElBruno.LocalEmbeddings.Harrier.csproj`  
  **Finding:** Harrier uses `Microsoft.ML.OnnxRuntime` types (InferenceSession, SessionOptions, etc.) and `Microsoft.ML.Tokenizers` (BpeTokenizer) via transitive dependencies from the base library. No direct `<PackageReference>` in Harrier's csproj. When published as a NuGet package, transitive deps flow correctly, but this is fragile for version pinning.  
  **Recommendation:** Add explicit `<PackageReference>` entries for `Microsoft.ML.OnnxRuntime` and `Microsoft.ML.Tokenizers` in the Harrier csproj to make the dependency explicit and version-locked.

### ⚠️ Improvement Needed — Package version spread

- **Finding:** Base/KernelMemory/VectorData at 1.0.1. Harrier/ImageEmbeddings at 1.0.0.  
  **Recommendation:** This is fine for now (Harrier is a new package). Ensure versions are bumped together on next release cycle.

---

## 5. Solution Structure

### ✅ Good — slnx organized correctly

- **File:** `ElBruno.LocalEmbeddings.slnx`  
  Properly organized into `/benchmarks/`, `/samples/`, `/src/`, `/tests/` folders. Harrier source and test projects both included. SharedModelTests included.

### ❌ Issue Found — Missing projects in slnx

- **File:** `ElBruno.LocalEmbeddings.slnx`  
  **Finding:** Two sample projects exist on disk but are absent from the solution:
  - `samples/DocumentRagFoundry/DocumentRagFoundry.csproj`
  - `samples/NpuBenchmarkSample/` (if it exists with a csproj)
  
  **Recommendation:** Add `DocumentRagFoundry` to the slnx under `/samples/`.

### ❌ Issue Found — Empty NPU directories

- **Files:** `src/ElBruno.LocalEmbeddings.Npu/`, `src/ElBruno.LocalEmbeddings.Npu.Intel/`, `src/ElBruno.LocalEmbeddings.Npu.Qualcomm/`  
  **Finding:** These directories contain only `bin/` and `obj/` artifacts — no `.csproj` files, no source code. Similarly, `tests/ElBruno.LocalEmbeddings.Npu.Tests/`, `tests/ElBruno.LocalEmbeddings.Npu.Intel.Tests/`, `tests/ElBruno.LocalEmbeddings.Npu.Qualcomm.Tests/` exist as directories.  
  **Recommendation:** Either clean up these directories or add a `.gitkeep` with a `README.md` noting planned future work. The `bin/obj` artifacts should be in `.gitignore`.

### ✅ Good — Harrier sample

- **File:** `samples/HarrierConsoleApp/`  
  Comprehensive 6-example sample demonstrating basic usage, batch generation, cosine similarity, instruction prefixes, and token counting. Properly included in slnx.

---

## Summary of Actionable Items

### Priority 1 (Should fix)
| # | Category | Item | File(s) |
|---|----------|------|---------|
| 1 | Docs | Add Harrier to README.md (Features, Install, Quick Start, Doc table) | `README.md` |
| 2 | Docs | Add Harrier to changelog | `docs/changelog.md` |
| 3 | Docs | Add HarrierConsoleApp to samples tables | `README.md`, `samples/README.md` |
| 4 | Solution | Add DocumentRagFoundry to slnx | `ElBruno.LocalEmbeddings.slnx` |
| 5 | Deps | Add explicit OnnxRuntime + Tokenizers refs in Harrier csproj | `src/.../Harrier.csproj` |

### Priority 2 (Should consider)
| # | Category | Item | File(s) |
|---|----------|------|---------|
| 6 | API | Add `AddHarrierEmbeddings(string modelName)` convenience overload | Harrier `ServiceCollectionExtensions.cs` |
| 7 | Docs | Add "Migration from MiniLM" section | `docs/harrier-integration.md` |
| 8 | Docs | Document DI registration conflict when both packages are used | Both `ServiceCollectionExtensions.cs` |
| 9 | Cleanup | Remove empty NPU stub directories or document as planned | `src/ElBruno.LocalEmbeddings.Npu*/` |

### Priority 3 (Nice to have)
| # | Category | Item | File(s) |
|---|----------|------|---------|
| 10 | API | Extract `IHarrierModelDownloader` interface | `HarrierModelDownloader.cs` |
| 11 | Security | Fix static HttpClient in both generators (SEC-002 gap) | `HarrierEmbeddingGenerator.cs`, `LocalEmbeddingGenerator.cs` |
| 12 | DI | Add IHttpClientFactory integration for Harrier DI path | Harrier `ServiceCollectionExtensions.cs` |
| 13 | Versions | Align package versions on next release cycle | All `.csproj` |

---

**Bottom line:** Harrier is architecturally solid — it follows every established pattern (M.E.AI interface, Options, DI extensions, security guards, perf optimizations). The main gaps are documentation visibility (README doesn't mention Harrier at all) and a few dependency hygiene items. The code itself is clean and ready for NuGet.
