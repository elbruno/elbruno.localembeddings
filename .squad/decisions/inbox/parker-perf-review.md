# Performance Analysis: ElBruno.LocalEmbeddings.Harrier

**By:** Parker (Performance Engineer)  
**Date:** 2025-07-17  
**Scope:** Comprehensive perf review of `src/ElBruno.LocalEmbeddings.Harrier/` comparing against base library patterns  
**Status:** Analysis only — no code changes

---

## 1. Memory Allocation Patterns (HarrierOnnxEmbeddingModel.cs)

### ✅ GOOD — ArrayPool usage matches base library best practice

`GenerateEmbeddings` (lines 186–218) uses `ArrayPool<long>.Shared.Rent/Return` in a `try/finally` block for `flatInputIds` and `flatAttentionMask`. Buffers are properly sliced to exact size via `.AsMemory(0, totalSize)`. Buffers are returned in all code paths via the finally block.

**Comparison to base:** Follows the exact same pattern established in PERF-01 for `OnnxEmbeddingModel`. No token_type_ids buffer needed (Harrier doesn't use it), so one fewer rental — slightly less allocation pressure than base.

### ✅ GOOD — No unnecessary allocations in hot path

The `ExtractEmbeddings` method (lines 224–240) correctly casts to `DenseTensor<float>`, gets a `Span` via `.Buffer.Span`, and slices per batch. The `.ToArray()` per embedding is unavoidable since each `float[]` must be independently owned by the caller.

### 🟡 MEDIUM — `outputTensor.Dimensions.ToArray()` in ExtractEmbeddings allocates unnecessarily

**File:** `HarrierOnnxEmbeddingModel.cs:226`

```csharp
var dimensions = outputTensor.Dimensions.ToArray();  // Allocates int[]
var embeddingDim = dimensions[^1];
```

`Dimensions` is a `ReadOnlySpan<int>`. The `.ToArray()` call allocates a heap `int[]` just to read the last element. Replace with:

```csharp
var embeddingDim = outputTensor.Dimensions[^1];
```

**Impact:** ~24 bytes per call (small array). Low per-call cost but trivial to fix.

### 🟡 MEDIUM — No Span<T>/stackalloc opportunity in hot loops, but `List<NamedOnnxValue>` allocates per call

**File:** `HarrierOnnxEmbeddingModel.cs:201–205`

```csharp
var inputs = new List<NamedOnnxValue>
{
    NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
    NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor)
};
```

This allocates a `List<T>` + backing array per inference call. Since Harrier always has exactly 2 inputs, consider using a reusable array field `NamedOnnxValue[2]` or at minimum `new List<NamedOnnxValue>(2)` to avoid list growth. The base library has the same pattern but with 2–3 inputs.

**Impact:** ~56 bytes per call (List header + internal array). Minor but easy fix.

---

## 2. Tokenizer Performance (HarrierTokenizer.cs)

### ✅ GOOD — tokenizer.json parsing is done once at creation

`LoadFromTokenizerJson` (line 80) is called only during `HarrierTokenizer.Create()`, which is called once in `HarrierEmbeddingGenerator`'s constructor. The `BpeTokenizer` instance is stored in `_tokenizer` and reused for all subsequent `Tokenize` calls. No re-parsing.

### ✅ GOOD — BpeTokenizer creation is cached (singleton pattern)

The `_tokenizer` field is `readonly` and set once. Thread-safe after initialization per the documented contract.

### 🟡 MEDIUM — Instruction prefix string concatenation allocates on every Tokenize call

**File:** `HarrierTokenizer.cs:102–104`

```csharp
var inputText = !string.IsNullOrEmpty(_instructionPrefix)
    ? _instructionPrefix + text
    : text;
```

Every single `Tokenize()` call allocates a new concatenated string. For the default prefix `"Instruct: Retrieve semantically similar text\nQuery: "` (49 chars) + a typical 200-char input, that's a ~500-byte allocation per text.

**Optimization:** Use `string.Concat` (already optimized by the compiler for two operands, but worth verifying). Alternatively, if the `BpeTokenizer` supports `ReadOnlySpan<char>` input, tokenize the prefix separately and prepend the IDs. However, since BPE tokenization is context-sensitive, splitting may change results — measure first.

For batch scenarios (e.g., batch=100), this is 100 × ~500 bytes = ~50 KB of transient string allocations. Not critical but measurable.

**Impact:** ~500 bytes per Tokenize call. Compounding at batch scale.

### 🟡 MEDIUM — LoadFromTokenizerJson creates intermediate MemoryStreams for vocab and merges

**File:** `HarrierTokenizer.cs:227–268`

The JSON vocab is re-serialized into a `MemoryStream` via `Utf8JsonWriter`, and merges are written to another `MemoryStream` via `StreamWriter`. For a tokenizer.json that may be 5–10 MB, this creates substantial transient memory pressure during initialization.

**Mitigation:** This runs once at startup, so it's a cold-path cost. However, for very large tokenizer vocabularies (Gemma 3 has ~256K tokens), the intermediate buffers could be significant. Consider:
- Using `RecyclableMemoryStream` from Microsoft.IO if available
- Or accepting this as acceptable cold-path cost (recommended — don't optimize startup for marginal gains)

**Impact:** One-time ~10–20 MB transient allocation during initialization. Acceptable.

### ✅ GOOD — TokenizeBatch uses `IList<string>` pattern (avoids double-enumeration)

Line 160: `IList<string> textList = texts as IList<string> ?? texts.ToList();` — follows the established pattern from PERF-12/13.

### 🟡 MEDIUM — CountTokens allocates full inputIds array just to count attention mask

**File:** `HarrierTokenizer.cs:183–193`

```csharp
public int CountTokens(string text, int? maxLength = null)
{
    var (_, attentionMask) = Tokenize(text, maxLength);
    ...
}
```

`Tokenize` allocates both `long[8192]` for inputIds and `long[8192]` for attentionMask (at default maxLength). That's ~128 KB allocated just to count tokens. The inputIds array is discarded immediately.

**Optimization:** Add a lightweight `CountTokensOnly` method that calls `_tokenizer.EncodeToIds()` directly and counts the result + 2 (BOS/EOS), avoiding the full padded array allocation.

**Impact:** ~64 KB wasted per `CountTokens` call (the unused inputIds array). Significant if called frequently.

---

## 3. ONNX Inference Efficiency

### ✅ GOOD — Session options configured optimally

**File:** `HarrierOnnxEmbeddingModel.cs:78–84`

- `GraphOptimizationLevel.ORT_ENABLE_ALL` ✓
- Parallel/sequential configurable ✓
- Thread counts default to `Environment.ProcessorCount` ✓
- `using var sessionOptions` ensures disposal ✓

Matches the base library's PERF-03/15/16 patterns exactly.

### ✅ GOOD — Inference session created once and reused

The `_session` field is set once in `Load()` and reused for all subsequent `Run()` calls. Thread-safe per ORT documentation.

### ✅ GOOD — Batch processing is efficient

Single batched `_session.Run()` call per `GenerateEmbeddings` invocation. No per-item inference overhead.

### 🟡 MEDIUM — No warm-up call to avoid JIT/ORT compilation costs on first inference

**File:** `HarrierOnnxEmbeddingModel.cs` — `Load()` creates the session but doesn't run a dummy inference.

The first `Run()` call after session creation typically incurs:
1. ONNX Runtime graph optimization/compilation (if not pre-optimized)
2. JIT compilation of managed wrappers
3. Memory pool initialization inside ORT

**Recommendation:** Add an optional `warmUp` parameter to `Load()` that runs a single dummy inference with minimal-size input. This shifts the cold-start cost from the first real user request to initialization time.

**Impact:** First inference call can be 2–10× slower than subsequent calls. Important for latency-sensitive applications.

### 🟢 LOW — Thread count defaults could be more conservative

Using `Environment.ProcessorCount` for both inter-op and intra-op threads is aggressive. For a 32-core machine, that's 32 × 32 = 1024 potential threads. The base library uses the same defaults, so this is consistent, but for Harrier (larger model, longer sequences), the memory overhead per-thread could be significant.

**Recommendation:** Consider capping `IntraOpNumThreads` at `Math.Min(ProcessorCount, 8)` by default, matching common ONNX Runtime guidance. Leave as configurable override for users who want full parallelism.

---

## 4. Model Download Performance

### ✅ GOOD — Download delegated to HuggingFaceDownloader (streaming)

The `ElBruno.HuggingFace.Downloader` package handles the actual download. Based on the usage pattern, it uses streaming downloads with `.tmp` files.

### ✅ GOOD — Progress reporting via `IProgress<T>` is allocation-light

The `Progress<DownloadProgress>` wrapper (line 100–103) converts the download progress. `IProgress<T>` implementations capture the current `SynchronizationContext` once, so the per-report overhead is minimal.

### 🔴 HIGH — File move operation uses `File.Move` (potentially cross-volume copy)

**File:** `HarrierModelDownloader.cs:116–127`

```csharp
var onnxSubDir = Path.Combine(modelDirectory, "onnx");
if (Directory.Exists(onnxSubDir))
{
    foreach (var file in Directory.GetFiles(onnxSubDir))
    {
        var destPath = Path.Combine(modelDirectory, Path.GetFileName(file));
        if (!File.Exists(destPath))
        {
            File.Move(file, destPath);
        }
    }
}
```

**Issues:**
1. `Directory.GetFiles(onnxSubDir)` with no filter returns ALL files, including the potentially huge `.onnx_data` file (~500 MB+). `File.Move` is fast on the same volume (rename), but the Harrier model includes external weight files (`model.onnx_data`) that could be very large.
2. Unlike the base library which filters with `"*.onnx"`, this moves ALL files from the `onnx/` subdirectory — including the `.onnx_data` file. This is actually correct for Harrier (it needs the data file adjacent to the model), but the lack of filter means any unexpected files would also be moved.
3. The `.onnx_data` file move should be a rename (same volume, same filesystem) — verify this is the case. If `File.Move` crosses a volume boundary, it becomes a copy+delete, which for a 500 MB file is catastrophic.

**Recommendation:** This is functionally correct but should:
- Verify the move stays on the same volume (it should, since both paths are under `modelDirectory`)
- Add a comment documenting why all files are moved (not just `*.onnx`)

**Impact:** The file move itself is a rename on the same volume (instant). However, if the cache directory is on a different volume than temp storage, this could be slow. Actual risk is LOW given the paths are both under `modelDirectory`. **Downgrading to ✅ GOOD after analysis — same volume rename is guaranteed.**

### 🟡 MEDIUM — No concurrent download lock (unlike base ModelDownloader)

**File:** `HarrierModelDownloader.cs:59` — `EnsureModelAsync` has no `SemaphoreSlim` concurrency guard.

The base `ModelDownloader` uses `ConcurrentDictionary<string, SemaphoreSlim>` to serialize concurrent downloads of the same model. `HarrierModelDownloader` does not. If two `HarrierEmbeddingGenerator` instances are created concurrently for the same model, they may race on the download, causing `.tmp` file conflicts.

**Recommendation:** Add the same `_downloadLocks` pattern from the base `ModelDownloader`.

**Impact:** Correctness issue in concurrent scenarios, not strictly a performance issue. Including here because it was a deliberate pattern in the base library.

### 🟡 MEDIUM — SHA-256 hash computation reads the entire ONNX model file twice

**File:** `HarrierModelDownloader.cs:144–146`

```csharp
WriteSidecarHash(finalModelPath);  // Reads file → computes SHA-256 → writes .sha256
```

`WriteSidecarHash` calls `ComputeSha256` which reads the entire file. If the file is 500 MB (FP32 model + data), this is a 500 MB sequential read. Then if `_options.ExpectedHash` is set (line 149), `ComputeSha256` is called again — another 500 MB read.

**Optimization:** Compute the hash once and reuse:

```csharp
var actualHash = ComputeSha256(finalModelPath);
File.WriteAllText(finalModelPath + ".sha256", actualHash);
if (_options.ExpectedHash != null && !string.Equals(actualHash, _options.ExpectedHash, ...)) { ... }
```

**Impact:** Saves ~500 MB of I/O when ExpectedHash is set. Even without it, the single read is still significant at ~500 MB for FP32 models (quantized models are smaller).

---

## 5. Benchmarks Coverage

### 🔴 HIGH — Zero benchmarks exist for the Harrier package

**File:** `benchmarks/ElBruno.LocalEmbeddings.Benchmarks/ElBruno.LocalEmbeddings.Benchmarks.csproj`

The benchmark project only references `ElBruno.LocalEmbeddings`. There are no Harrier-specific benchmarks in the entire `benchmarks/` folder. `grep -r "Harrier" benchmarks/` returns zero matches.

This is a significant gap because Harrier has fundamentally different characteristics from the base library:
- Decoder-only architecture (vs. encoder-only BERT-style)
- 640-dim embeddings (vs. 384-dim for MiniLM)
- 8192 default sequence length (vs. 512) — **16× more tokens per sequence**
- BPE tokenizer (vs. BERT WordPiece)
- No mean pooling needed (baked into graph)
- External weight files (.onnx_data)

### Suggested Benchmarks

The following BenchmarkDotNet classes should be added to `benchmarks/ElBruno.LocalEmbeddings.Benchmarks/`:

**1. `HarrierTokenizerBenchmarks`** — Critical, unique tokenizer path
```csharp
[MemoryDiagnoser]
public class HarrierTokenizerBenchmarks
{
    [Benchmark] public void TokenizeShortText()    // "Hello world" — measures per-call overhead
    [Benchmark] public void TokenizeLongText()     // 500-word paragraph — measures scaling
    [Benchmark] public void TokenizeBatch10()      // 10 items — measures batch overhead
    [Benchmark] public void TokenizeWithPrefix()   // With default instruction prefix
    [Benchmark] public void TokenizeWithoutPrefix() // Without prefix — isolates prefix cost
    [Benchmark] public void CountTokens()          // CountTokens path (wasteful allocation?)
}
```

**2. `HarrierEmbeddingGenerationBenchmarks`** — End-to-end throughput
```csharp
[MemoryDiagnoser]
public class HarrierEmbeddingGenerationBenchmarks
{
    [Benchmark] public void SingleEmbedding()
    [Benchmark] public void Batch10()
    [Benchmark] public void Batch100()
    [Params(128, 512, 2048, 8192)] public int SequenceLength { get; set; }
}
```

**3. `HarrierModelLoadingBenchmarks`** — Cold vs. warm load
```csharp
public class HarrierModelLoadingBenchmarks
{
    [Benchmark] public void ColdLoad()   // First load from disk
    [Benchmark] public void WarmLoad()   // Subsequent load (OS cache warm)
}
```

**4. `HarrierExtractEmbeddingsBenchmarks`** — Isolated extraction (no model required)
```csharp
[MemoryDiagnoser]
public class HarrierExtractEmbeddingsBenchmarks
{
    [Benchmark] public void ExtractBatch1()
    [Benchmark] public void ExtractBatch10()
    [Benchmark] public void ExtractBatch100()
    // Uses synthetic DenseTensor data — no ONNX session required
}
```

**5. `HarrierVsBaseBenchmarks`** — Head-to-head comparison
```csharp
[MemoryDiagnoser]
public class HarrierVsBaseBenchmarks
{
    [Benchmark(Baseline = true)] public void BaseLibrarySingleEmbed()
    [Benchmark] public void HarrierSingleEmbed()
    // Compare allocation, throughput, latency
}
```

**Impact:** Without benchmarks, performance regressions in the Harrier package will be undetectable. This is the most critical gap.

---

## 6. NuGet Package Size Analysis

### ✅ GOOD — Harrier package has minimal direct dependencies

**File:** `ElBruno.LocalEmbeddings.Harrier.csproj`

The Harrier package has a single `<ProjectReference>` to the base `ElBruno.LocalEmbeddings` project. It adds no additional NuGet package dependencies of its own. All heavy dependencies (ONNX Runtime, ML.Tokenizers, HuggingFace.Downloader) flow through the base package.

**Projected package structure:**
- Harrier DLL: ~30–50 KB (4 source files, clean code)
- No native binaries (ONNX Runtime comes from base)
- No bundled models (downloaded at runtime)
- README + icon: ~100 KB

**Total projected NuGet package size: ~150–200 KB** (excluding transitive dependencies)

### 🟢 LOW — Base library pulls in ~200 MB of ONNX Runtime native binaries transitively

This is inherited, not Harrier-specific. But consumers installing Harrier get the same ONNX Runtime native binary payload. No action needed — this is inherent to the ONNX Runtime dependency.

### 🟡 MEDIUM — Harrier csproj includes README.md from root — verify it's the right README

**File:** `ElBruno.LocalEmbeddings.Harrier.csproj:26`

```xml
<None Include="..\..\README.md" Pack="true" PackagePath="\" />
```

This packs the **repository root** README.md into the Harrier NuGet package. This may not be ideal — the root README focuses on the base library. A Harrier-specific README would be better for NuGet gallery presentation.

**Impact:** Not a performance issue but affects package quality.

---

## 7. Startup Cost

### 🟡 MEDIUM — Harrier initialization is significantly heavier than base library

**Startup sequence for `HarrierEmbeddingGenerator.CreateAsync()`:**

1. **Model download** (first run only): ~500 MB for FP32, ~125 MB for quantized. Network-bound.
2. **SHA-256 sidecar write**: Reads entire model file (~125–500 MB) to compute hash.
3. **File moves**: Renames files from `onnx/` subdir to model root. Fast (same volume).
4. **`HarrierOnnxEmbeddingModel.Load()`**: Creates `InferenceSession`. This loads the ONNX graph into memory and runs graph optimization. For a 270M-parameter model, this likely takes **1–5 seconds**.
5. **`HarrierTokenizer.Create()`**: Parses `tokenizer.json` (~5–10 MB), extracts vocab and merges, creates `BpeTokenizer`. Likely takes **0.5–2 seconds**.
6. **No warm-up inference**: First actual user call pays JIT + ORT compilation cost.

**Total estimated cold startup: 2–7 seconds** (model already cached, no download needed)  
**Total estimated first-inference: additional 1–3 seconds** on first call

Compare to base library: ~0.5–1 second startup (smaller model, simpler tokenizer).

### 🟡 MEDIUM — Could lazy-defer tokenizer initialization

The tokenizer is created eagerly in the constructor even though it's only needed when `GenerateAsync` or `CountTokens` is called. For scenarios where the generator is registered in DI but not immediately used, lazy initialization could shave 0.5–2 seconds off startup.

**Recommendation:** Consider `Lazy<HarrierTokenizer>` pattern:

```csharp
private readonly Lazy<HarrierTokenizer> _tokenizer;
// Initialize in constructor:
_tokenizer = new Lazy<HarrierTokenizer>(() => HarrierTokenizer.Create(modelDirectory, options.MaxSequenceLength, options.InstructionPrefix));
```

**Trade-off:** Adds latency to first `GenerateAsync` call. Arguably worse for predictability. **Not recommended** unless startup time becomes a documented pain point.

### ✅ GOOD — Async `CreateAsync` pattern avoids blocking

`HarrierEmbeddingGenerator.CreateAsync()` uses `async/await` with `ConfigureAwait(false)` throughout. No sync-over-async in the primary factory path.

### 🟡 MEDIUM — DI factory uses sync-over-async (matches base library pattern)

**File:** `ServiceCollectionExtensions.cs:107`

```csharp
return HarrierEmbeddingGenerator.CreateAsync(options).GetAwaiter().GetResult();
```

This is documented and matches the base library decision (PERF-04/05). Acceptable for console/desktop apps but dangerous in ASP.NET Core. The documentation correctly warns about this.

---

## 8. Additional Findings

### 🔴 HIGH — Default MaxSequenceLength of 8192 causes massive allocation per Tokenize call

**File:** `HarrierEmbeddingsOptions.cs:43`

```csharp
public int MaxSequenceLength { get; set; } = 8192;
```

Each `Tokenize()` call allocates:
- `inputIds`: `new long[8192]` = 64 KB
- `attentionMask`: `new long[8192]` = 64 KB

**Per text: 128 KB of allocations just for tokenizer output.**

For a batch of 100 texts: **12.8 MB** of `long[]` arrays, most of which is zero-padding.

The base library uses `maxLength = 512`, resulting in only 8 KB per text (16× less).

Then in `GenerateEmbeddings`, these are flattened via ArrayPool:
- `flatInputIds`: `ArrayPool.Rent(batch * 8192)` — for batch=100, that's 6.4 MB
- `flatAttentionMask`: another 6.4 MB

**Total allocation per batch=100 at default settings: ~25.6 MB**

**Recommendations:**
1. Consider dynamic sequence length: tokenize first to find actual max token count, then re-pad to that length rather than the full 8192.
2. Use ArrayPool for the per-text tokenizer output arrays (inputIds, attentionMask) instead of `new long[maxLength]`.
3. At minimum, document this in the options: "Set MaxSequenceLength to the shortest value that covers your inputs to minimize memory usage."

**Impact:** ~128 KB per text × batch size. At batch=100, this is 12.8 MB of GC pressure per inference call. This is the single largest performance gap vs. the base library.

### 🟡 MEDIUM — Static `SharedModelDownloadHttpClient` in HarrierEmbeddingGenerator doesn't use SocketsHttpHandler

**File:** `HarrierEmbeddingGenerator.cs:26`

```csharp
private static readonly HttpClient SharedModelDownloadHttpClient = new();
```

The base library's `ModelDownloader()` parameterless constructor uses `new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) }` (SEC-002 fix). But `HarrierEmbeddingGenerator` creates a bare `new HttpClient()` which it passes to `HarrierModelDownloader(HttpClient, options)`.

Meanwhile, `HarrierModelDownloader`'s parameterless constructor correctly uses `SocketsHttpHandler`. But `HarrierEmbeddingGenerator.ResolveModelDirectoryAsync` bypasses that by passing in the static `SharedModelDownloadHttpClient`.

**Impact:** DNS rotation issue on long-running processes. Not a perf issue per se, but the base library fixed this in SEC-002.

---

## Summary by Impact

| # | Impact | Finding | Location |
|---|--------|---------|----------|
| 1 | 🔴 HIGH | Default 8192 MaxSequenceLength causes ~128 KB allocation per text | HarrierTokenizer + HarrierEmbeddingsOptions |
| 2 | 🔴 HIGH | Zero Harrier benchmarks exist | benchmarks/ |
| 3 | 🟡 MEDIUM | Instruction prefix string concatenation on every Tokenize | HarrierTokenizer.cs:102 |
| 4 | 🟡 MEDIUM | CountTokens allocates unused inputIds array (64 KB wasted) | HarrierTokenizer.cs:183 |
| 5 | 🟡 MEDIUM | SHA-256 computed twice when ExpectedHash is set | HarrierModelDownloader.cs:144–155 |
| 6 | 🟡 MEDIUM | No concurrent download lock (race condition) | HarrierModelDownloader.cs:59 |
| 7 | 🟡 MEDIUM | No warm-up inference for first-call latency | HarrierOnnxEmbeddingModel.cs |
| 8 | 🟡 MEDIUM | Static HttpClient missing SocketsHttpHandler (SEC-002 gap) | HarrierEmbeddingGenerator.cs:26 |
| 9 | 🟡 MEDIUM | `Dimensions.ToArray()` unnecessary allocation in ExtractEmbeddings | HarrierOnnxEmbeddingModel.cs:226 |
| 10 | 🟡 MEDIUM | `List<NamedOnnxValue>` allocated per inference call | HarrierOnnxEmbeddingModel.cs:201 |
| 11 | 🟡 MEDIUM | Harrier NuGet packs root README instead of Harrier-specific | Harrier.csproj:26 |
| 12 | 🟢 LOW | Thread count defaults could be capped | HarrierOnnxEmbeddingModel.cs:74 |
| 13 | 🟢 LOW | LoadFromTokenizerJson transient memory during init | HarrierTokenizer.cs:227 |
| 14 | ✅ GOOD | ArrayPool usage in GenerateEmbeddings | HarrierOnnxEmbeddingModel.cs:186 |
| 15 | ✅ GOOD | Session options configuration | HarrierOnnxEmbeddingModel.cs:78 |
| 16 | ✅ GOOD | Session singleton reuse | HarrierOnnxEmbeddingModel.cs:24 |
| 17 | ✅ GOOD | Batch inference efficiency | HarrierOnnxEmbeddingModel.cs:207 |
| 18 | ✅ GOOD | Tokenizer JSON parsed once | HarrierTokenizer.cs:80 |
| 19 | ✅ GOOD | TokenizeBatch IList pattern | HarrierTokenizer.cs:160 |
| 20 | ✅ GOOD | Async CreateAsync pattern | HarrierEmbeddingGenerator.cs:93 |
| 21 | ✅ GOOD | Minimal NuGet dependency footprint | Harrier.csproj |
| 22 | ✅ GOOD | ExtractEmbeddings uses Span slicing | HarrierOnnxEmbeddingModel.cs:231 |
| 23 | ✅ GOOD | File download delegated to HuggingFaceDownloader | HarrierModelDownloader.cs:106 |

**Overall Assessment:** The Harrier package follows base library patterns well. The two HIGH findings (8192 allocation pressure and missing benchmarks) should be addressed before the package ships. The MEDIUM findings are worth pursuing in a follow-up pass.
