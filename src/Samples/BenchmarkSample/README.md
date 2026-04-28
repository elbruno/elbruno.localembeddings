# BenchmarkSample — BenchmarkDotNet Performance Suite

Reproducible performance benchmarks for **ElBruno.LocalEmbeddings** using [BenchmarkDotNet](https://benchmarkdotnet.org/).

## Prerequisites

- .NET 10 SDK
- Model downloaded (runs automatically on first use, or pre-download via the ConsoleApp sample)

## Running Benchmarks

### All benchmarks

```bash
cd samples/BenchmarkSample
dotnet run -c Release
```

### Specific benchmark class

```bash
dotnet run -c Release -- --filter "*EmbeddingBenchmarks*"
dotnet run -c Release -- --filter "*SimilarityBenchmarks*"
dotnet run -c Release -- --filter "*TokenizerBenchmarks*"
```

### Export results as JSON (for baseline comparison)

```bash
dotnet run -c Release -- --exporters json
```

Results are written to `BenchmarkDotNet.Artifacts/` by default.

## Benchmark Scenarios

| Class | Method | What it measures |
|-------|--------|------------------|
| `EmbeddingBenchmarks` | `SingleEmbedding` | Single-text embedding throughput |
| `EmbeddingBenchmarks` | `BatchEmbedding(10/50/100)` | Batch throughput scaling |
| `SimilarityBenchmarks` | `CosineSimilarity384` | TensorPrimitives cosine similarity (384-dim) |
| `SimilarityBenchmarks` | `CosineSimilarity768` | TensorPrimitives cosine similarity (768-dim) |
| `SimilarityBenchmarks` | `FindClosest100` | Nearest-neighbour search in 100 documents |
| `SimilarityBenchmarks` | `FindClosest1000` | Nearest-neighbour search in 1,000 documents |
| `TokenizerBenchmarks` | `TokenizeShort` | Single short-text tokenization |
| `TokenizerBenchmarks` | `TokenizeLong` | Single long-text tokenization |
| `TokenizerBenchmarks` | `TokenizeBatch(10/50)` | Batch tokenization scaling |

## Baseline Strategy

See [baselines/README.md](baselines/README.md) for the full cross-platform baseline workflow.

### Quick start

```bash
# Capture baseline on Windows
dotnet run -c Release -- --exporters json --artifacts baselines/windows-x64

# Capture baseline on Linux
dotnet run -c Release -- --exporters json --artifacts baselines/linux-x64

# Compare a new run against stored baseline
dotnet run -c Release -- --exporters json
# Then diff BenchmarkDotNet.Artifacts/ against baselines/<platform>/
```

## Notes

- Always run with `-c Release` for accurate results. Debug builds include JIT overhead and are not representative.
- The `[GlobalSetup]` method loads the ONNX model once; model loading time is not included in individual benchmark iterations.
- `[MemoryDiagnoser]` is enabled on all benchmarks to track allocations.
- The `BenchmarkDotNet.Artifacts/` directory is gitignored and not committed to the repository.
