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
