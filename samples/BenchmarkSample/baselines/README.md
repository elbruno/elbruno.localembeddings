# Benchmark Baselines

This directory stores BenchmarkDotNet JSON result exports that serve as performance baselines for regression detection.

## Directory Structure

```
baselines/
├── README.md                    ← This file
├── windows-x64/                 ← Windows x64 baseline results
│   └── results/
│       └── *.json
└── linux-x64/                   ← Linux x64 baseline results
    └── results/
        └── *.json
```

## Capturing a Baseline

Run benchmarks in Release mode with JSON export, directing output to the appropriate platform directory:

### Windows (x64)

```powershell
cd samples/BenchmarkSample
dotnet run -c Release -- --exporters json --artifacts baselines/windows-x64
```

### Linux (x64)

```bash
cd samples/BenchmarkSample
dotnet run -c Release -- --exporters json --artifacts baselines/linux-x64
```

### Linux (ARM64)

```bash
cd samples/BenchmarkSample
dotnet run -c Release -- --exporters json --artifacts baselines/linux-arm64
```

## Comparing Against a Baseline

### Manual comparison

1. Run benchmarks with JSON export:

   ```bash
   dotnet run -c Release -- --exporters json
   ```

2. Open `BenchmarkDotNet.Artifacts/results/` and compare mean values against the stored baseline in `baselines/<platform>/results/`.
3. Flag any benchmark where the new mean exceeds the baseline mean by more than **5%**.

### Using dotnet/performance ResultComparer (recommended)

The [dotnet/performance](https://github.com/dotnet/performance) repository provides a `ResultComparer` tool:

```bash
git clone https://github.com/dotnet/performance.git
cd performance/src/tools/ResultsComparer

dotnet run -- \
  --base <path-to-baselines>/<platform>/results \
  --diff <path-to-new>/BenchmarkDotNet.Artifacts/results \
  --threshold 5%
```

This produces a report showing which benchmarks improved, regressed, or remained stable.

## When to Update Baselines

Update baselines when:

- **Intentional performance changes** land (e.g., SIMD optimizations, batch improvements)
- **Hardware changes** occur on the benchmark machine
- **Runtime upgrades** happen (e.g., .NET 10 → .NET 11)

Do **not** update baselines to hide regressions.

## CI Integration (Future)

A GitHub Actions workflow can automate this:

1. **Trigger:** On PR or scheduled nightly.
2. **Matrix:** `windows-latest` and `ubuntu-latest`.
3. **Steps:**
   - Checkout the repo.
   - Run `dotnet run -c Release -- --exporters json`.
   - Download the stored baseline from `baselines/`.
   - Compare using ResultComparer or a custom script.
   - Post results as a PR comment.
4. **Artifacts:** Upload the new JSON results as GitHub Actions artifacts for traceability.

This keeps `.github/` workflows separate from the benchmark sample itself.

## Key Metrics to Track

| Metric | Source | Significance |
|--------|--------|--------------|
| Mean | BenchmarkDotNet | Average execution time per operation |
| Allocated | MemoryDiagnoser | Bytes allocated per operation |
| StdDev | BenchmarkDotNet | Measurement stability indicator |
| Median | BenchmarkDotNet | More robust than mean for skewed distributions |
