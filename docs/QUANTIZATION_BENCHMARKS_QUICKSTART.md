# Quick Start: Running Quantization Benchmarks

## One-Minute Setup

```bash
# Navigate to benchmarks directory
cd src/ElBruno.LocalEmbeddings.Benchmarks

# Run benchmarks (generates HTML report)
dotnet run --configuration Release --framework net8.0

# View results
# → Check BenchmarkDotNet.Artifacts/results/ for HTML report
```

## What Gets Measured

| Benchmark | What It Does | Why It Matters |
|-----------|-------------|----------------|
| `FullPrecision_SingleEmbedding` | Generate 1 embedding with FP32 | Baseline latency |
| `Quantized_SingleEmbedding` | Generate 1 embedding with INT8 | Speedup per item |
| `FullPrecision_Batch10` | Generate 10 embeddings with FP32 | Real-world small batch |
| `Quantized_Batch10` | Generate 10 embeddings with INT8 | Practical speedup |
| `FullPrecision_Batch32` | Generate 32 embeddings with FP32 | Typical batch size |
| `Quantized_Batch32` | Generate 32 embeddings with INT8 | Production speedup |
| `FullPrecision_Batch100` | Generate 100 embeddings with FP32 | Peak throughput |
| `Quantized_Batch100` | Generate 100 embeddings with INT8 | Maximum gains |

## Expected Output

### Console Summary
```
BenchmarkDotNet v0.15.8
runtime=.NET 8.0.27, cpu=...

| Method                        | Mean      | Error    |
|-------------------------------|-----------|----------|
| FullPrecision_SingleEmbedding | 45.23 ms  | 0.81 ms  |
| Quantized_SingleEmbedding     | 32.05 ms  | 0.52 ms  | ✅ 29% faster
| FullPrecision_Batch32         | 1250 ms   | 22 ms    |
| Quantized_Batch32             |  975 ms   | 18 ms    | ✅ 22% faster
```

### HTML Report
- Located: `BenchmarkDotNet.Artifacts/results/QuantizedVsFullBenchmarks-report.html`
- Contains: Interactive charts, memory diagnostics, statistical analysis
- Use case: Publish to documentation site or marketing materials

## Accuracy Check

The benchmark includes **cosine similarity measurement** to verify quantization doesn't hurt accuracy:

```csharp
var similarity = benchmarks.GetAccuracyMetric();
// Returns: 0.9876 (1.0 = identical, >0.95 = acceptable)
```

**Success Threshold:** > 0.95 (max 5% embedding drift)

## Prerequisites

### 1. Cached Models Required

**On Windows:**
```
C:\Users\{YourUsername}\AppData\Local\LocalEmbeddings\models\
  └── sentence-transformers_all-MiniLM-L6-v2\
      ├── model.onnx              (100 MB, full-precision)
      ├── model_int8.onnx         (26 MB, quantized) ← Key file
      ├── tokenizer.json
      └── ...
```

**On Linux/macOS:**
```
~/.local/share/LocalEmbeddings/models/
  └── sentence-transformers_all-MiniLM-L6-v2/
      ├── model.onnx
      ├── model_int8.onnx
      └── ...
```

### 2. Create INT8 Quantized Model

If `model_int8.onnx` doesn't exist, create it:

```bash
# Using ONNX Model Quantizer
pip install onnxruntime-tools

python -m onnxruntime.quantization.quantize \
  --model_input model.onnx \
  --model_output model_int8.onnx \
  --quant_mode dynamic \
  --per_channel
```

Or download from [Hugging Face](https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2).

## Troubleshooting

### "Benchmark skipped - no models found"
→ Cache the models to the correct directory (see Prerequisites above)

### "Build failed"
→ Ensure .NET 8.0 or higher is installed: `dotnet --version`

### "Process exited with exit code 1"
→ Specify framework: `--framework net8.0`

## Advanced Usage

### Run Specific Benchmark Only
```bash
dotnet run --configuration Release --framework net8.0 -- \
  --filter "*Batch32*"
```

### Generate Markdown Report
```bash
dotnet run --configuration Release --framework net8.0 -- \
  --format markdown > report.md
```

### Reduce Iterations (Faster, Less Accurate)
```bash
dotnet run --configuration Release --framework net8.0 -- \
  -i 5  # Only 5 iterations instead of ~100
```

## Success Criteria

✅ **Performance:** INT8 ≤ 80% of FP32 latency (≥20% speedup)  
✅ **Accuracy:** Cosine similarity ≥ 0.95 (≤5% drift)  
✅ **Memory:** INT8 model ≤ 30% of FP32 model size  

## Files

| Path | Purpose |
|------|---------|
| `src/ElBruno.LocalEmbeddings.Benchmarks/QuantizedVsFullBenchmarks.cs` | Benchmark code |
| `docs/quantization-benchmarks-methodology.md` | Detailed methodology |
| `docs/QUANTIZATION_BENCHMARKS_SUMMARY.md` | Full report & results |

## Contact

**Parker — Performance Engineer**
- Benchmarks, profiling, optimization decisions
- Slack: @parker
