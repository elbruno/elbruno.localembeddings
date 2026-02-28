# Team Decisions

Shared decisions all agents must respect. Scribe merges new decisions from the inbox.

<!-- Decisions are appended below. Each starts with ### -->

## 2026-02-12: Solution Structure and API Surface

**By:** Ripley  
**Status:** Established

Established the project structure with `LocalEmbeddingGenerator` as the main public type implementing `IEmbeddingGenerator<string, Embedding<float>>`. Internal types (`OnnxEmbeddingModel`, `ModelDownloader`) are not exposed. DI registration via `AddLocalEmbeddings()` extension method.

**Rationale:** Following M.E.AI patterns ensures the library integrates seamlessly with the .NET AI ecosystem. Keeping ONNX internals private allows implementation changes without breaking consumers.

---

## 2026-02-12: ModelDownloader Design Decisions

**By:** Dallas (Core Dev)

### Cache Path Strategy
- Windows uses `%LOCALAPPDATA%\LocalEmbeddings\models\`
- Linux/macOS uses XDG_DATA_HOME (defaulting to `~/.local/share/LocalEmbeddings/models/`)
- Model names are sanitized (slashes → underscores) for path safety

### HuggingFace Download URLs
- ONNX model: `https://huggingface.co/{model}/resolve/main/onnx/model.onnx`
- Tokenizer files: `https://huggingface.co/{model}/resolve/main/{file}`
- The `/onnx/model.onnx` path is standard for sentence-transformers models

### Caching Behavior
- Simple existence check (no hash verification currently)
- Uses `.tmp` files during download to prevent partial file corruption
- Tokenizer files are optional — missing files don't fail the download

### Interface Added
- `IModelDownloader` interface enables DI and unit testing
- Both interface and class are public for direct usage

---

## 2026-02-12: InMemoryVectorStore Pattern for Samples

**By:** Kane (Integration Developer)  
**Status:** Implemented

Created `InMemoryVectorStore` that takes `IEmbeddingGenerator<string, Embedding<float>>` via constructor injection for RAG-style samples.

### Key Design
- Constructor injection allows clean DI registration: `services.AddSingleton<InMemoryVectorStore>()`
- `AddDocumentsAsync()` with `Action<int, int>? progressCallback` for batch loading with progress
- `SearchAsync()` returns `List<SearchResult>` with document and similarity score
- Uses cosine similarity for relevance ranking

**Rationale:** Progress callback pattern avoids coupling to specific UI frameworks. SearchResult as separate type keeps Document clean and allows future metadata expansion.

---

## 2026-02-13: L2 Normalization Default

**By:** Dallas (Core Dev)  
**Status:** Implemented

Added `NormalizeEmbeddings` option to `LocalEmbeddingsOptions` to control whether embeddings are L2-normalized to unit length.

### Decision
**Default is `false` (no normalization)** to maintain backward compatibility.

### Rationale
1. **Breaking change avoidance**: Changing vector magnitudes would affect existing similarity scores
2. **Opt-in behavior**: Users who want sentence-transformers-compatible normalized vectors can enable it
3. **Performance consideration**: Normalization adds a small computational overhead

### Usage Note
When `NormalizeEmbeddings = true`:
- Cosine similarity equals dot product (faster computation in some scenarios)
- Vectors have magnitude 1.0
- Matches Python sentence-transformers default output

---

## 2026-02-28: Security Audit Findings — 9 Findings, Full Remediation Plan

**By:** Ash (Security Engineer)  
**Status:** Audited & Fully Remediated (Phases 1–4)

Full audit of all 5 source projects, 21 csproj files, and public API surface. No known CVEs found. No secrets in source. Nine findings identified and remediated across four phases.

### Summary of Findings (all resolved)

| ID | Severity | Description | Resolved |
|----|----------|-------------|---------|
| SEC-001 | HIGH | No integrity verification for downloaded ONNX model files | Phase 1 |
| SEC-002 | MEDIUM | Static HttpClient bypasses DNS rotation; socket exhaustion risk | Phase 4 |
| SEC-003 | MEDIUM | Path traversal via ImageEmbeddingsOptions file name properties | Phase 2 |
| SEC-004 | MEDIUM | Missing input validation in ClipImageEncoder/ClipTextEncoder | Phase 2 |
| SEC-005 | MEDIUM | Missing null guards in ImageSearchEngine | Phase 2 |
| SEC-006 | MEDIUM | Path traversal defense-in-depth for model cache names | Phase 1 |
| SEC-007 | LOW | Sync-over-async deadlock risk (undocumented) | Phase 4 |
| SEC-008 | LOW | OnnxRuntime 1.24.1 behind (1.24.2 available) | Phase 4 |
| SEC-009 | LOW | ClipTokenizer reads files without size limits | Phase 4 |

### Positive Findings
- `dotnet list package --vulnerable` — zero CVEs across all projects
- No secrets or credentials in source
- SixLabors.ImageSharp 3.1.12 — latest, no NuGet advisories
- DI path correctly uses `IHttpClientFactory`
- Nullable reference types and warnings-as-errors enforced globally

---

## 2026-02-28: SEC-001 — SHA-256 Sidecar Integrity Pattern

**By:** Ash (Security Engineer)  
**Status:** Implemented (Phase 1)

SHA-256 sidecar files (`{file}.sha256`) are written after every successful download of ONNX files in both `ModelDownloader` and `HuggingFaceImageModelDownloader`. On cache hit, sidecar is verified; mismatch deletes the corrupt file and triggers re-download.

### Key Details
- `LocalEmbeddingsOptions.ExpectedHash` (nullable `string?`) added for pinning expected hash post-download
- Legacy cached files (no sidecar) treated as valid for backward compatibility; sidecar written on next call
- `SHA256.HashData(stream)` used (preferred over `SHA256.Create()`, available .NET 5+)
- `IModelDownloader.EnsureModelAsync` gains optional `string? expectedHash = null` parameter

---

## 2026-02-28: SEC-006 — Path Traversal Defense-in-Depth

**By:** Ash (Security Engineer)  
**Status:** Implemented (Phase 1)

`ModelDownloader.EnsureModelAsync`: after `DefaultPathHelper.SanitizeModelName(modelName)`, the resolved directory is canonicalised with `Path.GetFullPath` and asserted to start with the canonicalised cache root (case-insensitive). Fires before any I/O.

### Key Note
`DefaultPathHelper.SanitizeModelName` converts `/` → `_`, so slash-based traversal names become safe subpaths. The guard catches bare `".."` inputs (no slash). Tests should use `".."` to directly exercise the guard.

---

## 2026-02-28: SEC-003/004/005 — ImageEmbeddings Input Validation

**By:** Ash (Security Engineer)  
**Status:** Implemented (Phase 2)

**SEC-003:** `ImageEmbeddingsOptions` file-name properties (`TextModelFileName`, `VisionModelFileName`, `VocabFileName`, `MergesFileName`) enforce via `ValidateFileName` static helper: (1) not null/whitespace, (2) no `..` sequences, (3) no `Path.GetInvalidFileNameChars()`.

**SEC-004:** `ClipImageEncoder(modelPath)` and `ClipTextEncoder(modelPath, vocabPath, mergesPath)` constructors: `ArgumentException.ThrowIfNullOrWhiteSpace` + `File.Exists` guard before `InferenceSession` creation. Throws `FileNotFoundException` with descriptive message.

**SEC-005:** `ImageSearchEngine` constructor: `ArgumentNullException.ThrowIfNull` for both encoders. `SearchByText` and `SearchByImage`: `ArgumentException.ThrowIfNullOrWhiteSpace` on string parameters.

---

## 2026-02-28: SEC-002/007/008/009 — Security Polish

**By:** Ash (Security Engineer)  
**Status:** Implemented (Phase 4)

**SEC-002:** `ModelDownloader()` parameterless ctor uses `new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) }`. DI/`IHttpClientFactory` path is preferred for production.

**SEC-007:** `LocalEmbeddingGenerator(LocalEmbeddingsOptions)` constructor gets `<remarks>` warning about sync-over-async deadlock risk. Inline `// Sync-over-async` comment at `.GetAwaiter().GetResult()`. Preserved for backward compatibility; `CreateAsync()` is the recommended alternative.

**SEC-008:** `Microsoft.ML.OnnxRuntime` bumped 1.24.1 → 1.24.2 in `ElBruno.LocalEmbeddings.csproj`, `ElBruno.LocalEmbeddings.ImageEmbeddings.csproj`, and `ElBruno.LocalEmbeddings.Tests.csproj`.

**SEC-009:** `ClipTokenizer` — 50 MB size guard for both vocab JSON and merges text files before read. `const long MaxVocabFileSizeBytes = 50 * 1024 * 1024`. Throws `InvalidOperationException` with file name and actual size in MB.

---

## 2026-02-28: Performance Audit Findings — 17 Findings

**By:** Parker (Performance Engineer)  
**Status:** Audited; Phases 1–4 complete, Phase 5 (benchmarks) in progress

Full codebase performance audit. 17 findings (2 HIGH, 12 MEDIUM, 3 LOW). All non-benchmark findings resolved.

### What's Working Well
- ONNX session reuse (singleton, thread-safe for concurrent inference)
- TensorPrimitives for cosine similarity and L2 normalization (SIMD-accelerated)
- Batched inference in `OnnxEmbeddingModel.GenerateEmbeddings`
- `ConfigureAwait(false)` used consistently
- Graph optimization level `ORT_ENABLE_ALL`
- `CreateAsync()` factory methods exist as the async-safe alternative

### Critical Findings Summary
- PERF-01/02 (HIGH): ArrayPool + SIMD mean pooling — resolved Phase 1
- PERF-03, 15/16 (MEDIUM): SessionOptions disposal + CLIP session options — resolved Phase 2
- PERF-09/10 (MEDIUM): Heap-based search replacing LINQ — resolved Phase 3
- PERF-06/07/08 (MEDIUM): CLIP output + tokenizer allocation reduction — resolved Phase 3
- PERF-12/13 (LOW): Redundant .ToList() removal — resolved Phase 3
- PERF-04/05 (MEDIUM): Async documentation — resolved Phase 4
- PERF-17 (MEDIUM): Benchmark expansion — Phase 5 in progress

---

## 2026-02-28: PERF-01/02 — ArrayPool + SIMD Mean Pooling

**By:** Parker (Performance Engineer)  
**Status:** Implemented (Phase 1)

**PERF-02:** `ApplyMeanPooling` in `OnnxEmbeddingModel.cs` — triple nested scalar loop replaced with `TensorPrimitives.Add` (accumulation) + `TensorPrimitives.Divide` (normalization). Uses `DenseTensor<float>.Buffer.Span` for flat contiguous slice per token. Inner `hidden` loop eliminated entirely.

**PERF-01:** `flatInputIds`, `flatAttentionMask`, `flatTokenTypeIds` use `ArrayPool<long>.Shared.Rent/Return` in `try/finally`. Arrays sliced to exact size via `.AsMemory(0, totalSize)`. `flatTokenTypeIds` explicitly zero-cleared. ~1.2 MB GC pressure eliminated per call at batch=100, seq=512.

---

## 2026-02-28: PERF-03/15/16 — SessionOptions Hygiene

**By:** Parker (Performance Engineer)  
**Status:** Implemented (Phase 2)

**PERF-03:** `OnnxEmbeddingModel.Load` — collapsed two try blocks into single `try` with `using var sessionOptions`. Success-path `SessionOptions` leak fixed. Safe because ORT copies options during `InferenceSession` construction.

**PERF-15/16:** Both `ClipImageEncoder` and `ClipTextEncoder` now use optimized `SessionOptions`: `GraphOptimizationLevel.ORT_ENABLE_ALL`, `ExecutionMode.ORT_SEQUENTIAL`, `InterOpNumThreads=1`, `IntraOpNumThreads=Environment.ProcessorCount`. Both use `using var sessionOptions`.

---

## 2026-02-28: PERF-09 — PriorityQueue Min-Heap Standard for Top-K Search

**By:** Parker (Performance Engineer)  
**Status:** Implemented (Phase 3)

`PriorityQueue<TElement, float>` min-heap is now the **standard pattern for all top-K searches** in this codebase. Applied to `EmbeddingExtensions.FindClosest` (both overloads) and `ImageSearchEngine.RankResults`.

- O(n log n) → O(n log k); significant win at large corpus / small topK
- `TryPeek(out _, out float lowestScore)` inspects minimum priority (not `Peek()` which returns element)
- `DequeueEnqueue` available .NET 8+ (safe for this project's targets)
- Second `FindClosest` overload: tiebreaker `ThenBy(index)` preserved via O(k log k) post-heap sort of topK items only
- Future search methods (e.g., VectorData store search) MUST follow this O(n log k) heap pattern

---

## 2026-02-28: PERF-06/07/08/12/13 — Allocation Reduction Patterns

**By:** Parker (Performance Engineer)  
**Status:** Implemented (Phase 3)

**PERF-06/07:** CLIP encoder output: `results.First().AsEnumerable<float>().ToArray()` → `results.First().AsTensor<float>().ToArray()`. Bypasses IEnumerable iterator; uses DenseTensor's optimized `ToArray()`.

**PERF-08:** `Tokenizer.Tokenize` — `IReadOnlyList<int>` from `EncodeToIds()` iterated directly; `int[]` intermediate allocation eliminated. ~100 allocations removed per batch=100 call.

**PERF-12/13:** Standard pattern for `IEnumerable<T>` accepting methods that need indexing: `input as IList<T> ?? input.ToList()`. Applied to `TokenizeBatch` and `GenerateAsync`.

---

## 2026-02-28: PERF-04/05 — Async Factory Pattern Documentation

**By:** Parker (Performance Engineer)  
**Status:** Implemented (Phase 4)

Sync-over-async patterns in `LocalEmbeddingGenerator` constructor and both `ServiceCollectionExtensions` are **preserved for backward compatibility**. Clear documentation added:

- `ServiceCollectionExtensions.AddLocalEmbeddings`: expanded `<remarks>` with sync-over-async warning + two-part `<example>` (standard DI + async pre-build pattern using `CreateAsync()` before `builder.Build()`)
- `CreateAsync(LocalEmbeddingsOptions, CancellationToken)`: enriched `<remarks>` with DI registration example
- `AddImageEmbeddings`: `<strong>Async-Safety Note</strong>` in remarks
- `AddLocalEmbeddingsCore` + `EnsureModels`: inline `// Sync-over-async` comments at call sites

**Decision:** `CreateAsync()` is the recommended entry point for ASP.NET Core, UI frameworks, and any async-first environment. The constructor is acceptable for console applications and background services.

---

### 20260228-113434: User directive

**By:** Bruno (via Copilot)  
**What:** If a task is simple or easy, always use a 0x (fast/cheap) model like gpt-5-mini. Never over-provision model tier for trivial work.  
**Why:** User request — captured for team memory. Overrides default model selection for low-complexity tasks.
