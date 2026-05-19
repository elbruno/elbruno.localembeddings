# AOT Validation Checklist — Phase 2

**For:** ElBruno.LocalEmbeddings  
**Purpose:** Ensure Native AOT readiness before Phase 2 implementation  
**Audience:** Implementation team, release manager, QA

---

## Pre-Implementation Review

### 1. Codebase Reflection Analysis

**Task:** Search codebase for reflection patterns that break AOT.

```bash
# Search for problematic patterns
grep -r "Type\.GetType" src/
grep -r "Reflection\.Invoke" src/
grep -r "Activator\.CreateInstance" src/
grep -r "Expression\.Compile" src/
grep -r "DynamicMethod" src/
grep -r "System\.Reflection\.Emit" src/
```

**Expected Result:**
- ✅ No matches in inference path (OnnxEmbeddingModel, Tokenizer, LocalEmbeddingGenerator)
- ⚠️ Matches only in options binding (already marked [RequiresUnreferencedCode])

**Responsible:** Implementation team  
**Status:** [ ] Not started [ ] In progress [ ] Complete

---

### 2. Trimming Warnings Baseline

**Task:** Run trimmer analyzer to capture current warnings.

```bash
cd src/ElBruno.LocalEmbeddings
dotnet publish -c Release -f net8.0 \
    -p:PublishAot=true \
    -p:SelfContained=true \
    --nologo | tee trimmer-report-baseline.txt
```

**Expected Result:**
- Trimmer report generated
- Capture warning count and categories
- Document which warnings are expected vs. unexpected

**Baseline Warnings (Acceptable):**
- [x] `RequiresUnreferencedCode` on AddLocalEmbeddings(IConfiguration)
- [ ] Any other warnings?

**Responsible:** Implementation team  
**Status:** [ ] Not started [ ] In progress [ ] Complete

---

### 3. Dependency Analysis

**Task:** Audit all NuGet dependencies for AOT compatibility.

| Package | Version | AOT Status | Notes |
|---------|---------|-----------|-------|
| Microsoft.ML.OnnxRuntime | 1.24.4 | ⚠️ Partial | Managed API is AOT-safe; native dll required at runtime |
| Microsoft.ML.Tokenizers | 2.0.0 | ✅ Full | Pure managed code, no reflection |
| Microsoft.Extensions.AI.Abstractions | 10.4.1 | ✅ Full | Interfaces only, no reflection |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.5 | ✅ Full | Interfaces only |
| Microsoft.Extensions.Options | 10.0.5 | ⚠️ Partial | Binding uses reflection (documented) |
| System.Numerics.Tensors | 10.0.5 | ✅ Full | Hardware-accelerated, no reflection |
| ElBruno.HuggingFace.Downloader | 0.6.0 | ? | **ACTION:** Verify with maintainer |

**Action Items:**
- [ ] Contact ElBruno.HuggingFace.Downloader maintainer for AOT status
- [ ] If not AOT-ready, document workaround (custom downloader for AOT)
- [ ] Verify ONNX Runtime native binaries strategy

**Responsible:** Architect / Integration lead  
**Status:** [ ] Not started [ ] In progress [ ] Complete

---

## Build & Compilation Tests

### 4. AOT Build for .NET 8

**Task:** Publish as native AOT application for .NET 8.

```bash
cd tests/ElBruno.LocalEmbeddings.Tests
dotnet publish -c Release -f net8.0 \
    -p:PublishAot=true \
    -p:SelfContained=true
```

**Success Criteria:**
- [ ] Build succeeds (0 errors, minimal warnings)
- [ ] Executable generated: `bin/Release/net8.0/publish/ElBruno.LocalEmbeddings.Tests`
- [ ] Executable size <150 MB (self-contained)
- [ ] No runtime errors on basic operations

**Commands to Verify:**
```bash
# Check executable exists
ls -lh bin/Release/net8.0/publish/ElBruno.LocalEmbeddings.Tests

# Test runs (should not load reflection)
bin/Release/net8.0/publish/ElBruno.LocalEmbeddings.Tests
```

**Responsible:** Implementation team  
**Status:** [ ] Not started [ ] In progress [ ] Complete

---

### 5. AOT Build for .NET 10

**Task:** Publish as native AOT application for .NET 10.

```bash
cd tests/ElBruno.LocalEmbeddings.Tests
dotnet publish -c Release -f net10.0 \
    -p:PublishAot=true \
    -p:SelfContained=true
```

**Success Criteria:**
- [ ] Build succeeds
- [ ] Executable generated (should be smaller than net8.0)
- [ ] No additional warnings vs. net8.0

**Responsible:** Implementation team  
**Status:** [ ] Not started [ ] In progress [ ] Complete

---

## Functional Testing

### 6. AOT Compatibility Tests

**Task:** Run AotCompatibilityTests to verify no reflection in inference path.

```bash
dotnet test tests/ElBruno.LocalEmbeddings.Tests \
    --filter "Category=AotCompatibility" \
    --logger "console;verbosity=detailed"
```

**Test Cases to Pass:**
- [ ] DirectInstantiation_Works
- [ ] DependencyInjectionWithDelegate_Works
- [ ] DependencyInjectionWithPrebuiltOptions_Works
- [ ] ConfigurationBinding_ThrowsRequiresUnreferencedCodeWarning

**Expected Result:**
- All 4 tests pass
- No reflection warnings during test execution

**Responsible:** QA / Test team  
**Status:** [ ] Not started [ ] In progress [ ] Complete

---

### 7. End-to-End AOT Test

**Task:** Test real embedding generation with AOT-compiled code.

**Test Harness (new file):**
`tests/ElBruno.LocalEmbeddings.Tests/AotE2eTests.cs`

```csharp
[Collection("E2E")]
public class AotE2eTests
{
    [SkippableFact]
    public async Task GenerateEmbedding_WithAotCompiledCode_Works()
    {
        var options = new LocalEmbeddingsOptions
        {
            ModelPath = ModelFixture.ModelDirectory,
            PreferQuantization = QuantizationFormat.Float32
        };

        var generator = new LocalEmbeddingGenerator(options);
        
        var result = await generator.GenerateAsync(["Hello world", "Test"]);
        
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, emb => Assert.NotNull(emb));
    }

    [SkippableFact]
    public async Task Quantization_WithAot_LoadsCorrectModel()
    {
        var options = new LocalEmbeddingsOptions
        {
            ModelPath = ModelFixture.ModelDirectory,
            PreferQuantization = QuantizationFormat.Int8
        };

        var generator = new LocalEmbeddingGenerator(options);
        
        var result = await generator.GenerateAsync(["Quantization test"]);
        
        Assert.NotNull(result);
        Assert.Single(result);
    }
}
```

**Test Execution:**
```bash
dotnet test tests/ElBruno.LocalEmbeddings.Tests \
    --filter "Category=E2E" \
    --logger "console;verbosity=detailed"
```

**Expected Result:**
- Both tests pass
- No runtime errors
- Model loads and generates embeddings

**Responsible:** QA / Test team  
**Status:** [ ] Not started [ ] In progress [ ] Complete

---

## Performance & Size Validation

### 8. Performance Overhead Test

**Task:** Compare performance: AOT vs. JIT.

**Test:**
```bash
# Run benchmarks
dotnet run -c Release --project tests/ElBruno.LocalEmbeddings.Benchmarks -- \
    --filter "*Embedding*" \
    --warmupCount 5 \
    --targetCount 20
```

**Metrics to Capture:**

| Benchmark | JIT (ns) | AOT (ns) | Overhead % |
|-----------|----------|----------|-----------|
| Generate 1 embedding | ? | ? | <5% ✅ |
| Generate 32 embeddings | ? | ? | <5% ✅ |
| Cosine similarity | ? | ? | <5% ✅ |

**Expected Result:**
- AOT overhead <5% vs. JIT
- Performance not significantly degraded

**Responsible:** Performance team  
**Status:** [ ] Not started [ ] In progress [ ] Complete

---

### 9. Binary Size Validation

**Task:** Verify AOT executable size is reasonable for deployment.

```bash
# Check AOT executable size
du -h bin/Release/net8.0/publish/ElBruno.LocalEmbeddings.Tests

# Check dependency sizes
du -h bin/Release/net8.0/publish/ | sort -h
```

**Expected Result:**

| Component | Target Size | Status |
|-----------|-------------|--------|
| AOT executable (net8.0) | <100 MB | [ ] Pass |
| AOT executable (net10.0) | <80 MB | [ ] Pass |
| ONNX Runtime native | <50 MB | [ ] Pass |
| Model (full precision) | ~134 MB | [ ] Pass |
| Model (INT8 quantized) | ~33 MB | [ ] Pass |
| **Total (Azure Functions)** | **<250 MB** | [ ] Pass |

**Responsible:** DevOps / Release team  
**Status:** [ ] Not started [ ] In progress [ ] Complete

---

## Integration Tests

### 10. Azure Functions Simulation

**Task:** Test AOT app in simulated Azure Functions environment.

**Test Project:** `samples/AzureFunctionsSample/` (new)

```bash
cd samples/AzureFunctionsSample
func start  # Local Azure Functions emulator
curl -X POST http://localhost:7071/api/GenerateEmbedding \
    -H "Content-Type: application/json" \
    -d '{"texts": ["hello", "world"]}'
```

**Expected Result:**
- [ ] Function starts successfully
- [ ] HTTP endpoint responds with embeddings
- [ ] No errors in function logs
- [ ] Cold-start time <2 seconds

**Responsible:** DevOps / Functions team  
**Status:** [ ] Not started [ ] In progress [ ] Complete

---

### 11. Docker Deployment Test

**Task:** Test AOT app in Docker container (simulates serverless environment).

**Dockerfile:**
```dockerfile
FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-jammy

COPY bin/Release/net8.0/publish /app
COPY model_quantized.onnx /app/

WORKDIR /app
ENTRYPOINT ["./ElBruno.LocalEmbeddings.Tests"]
```

**Test:**
```bash
docker build -t elbruno-aot .
docker run --rm elbr uno-aot
```

**Expected Result:**
- [ ] Container builds successfully
- [ ] Container runs without errors
- [ ] App initializes in <2 seconds

**Responsible:** DevOps / Container team  
**Status:** [ ] Not started [ ] In progress [ ] Complete

---

## Quantization Validation

### 12. INT8 Quantization Accuracy

**Task:** Verify INT8 quantized models maintain accuracy.

**Test:**
```bash
# Download INT8 quantized model variant
# Run similarity benchmarks against reference results
# Calculate accuracy drop percentage
```

**Metrics:**

| Dataset | Float32 Baseline | INT8 Accuracy | Drop % | Status |
|---------|-----------------|---------------|--------|--------|
| STS benchmark (English) | 86.5 | 85.2 | 1.3% | [ ] Pass |
| MTEB benchmark | ? | ? | <2% | [ ] Pass |

**Expected Result:**
- [ ] INT8 accuracy drop <2%
- [ ] Speed improvement 2-3x vs. Float32

**Responsible:** ML/Data team  
**Status:** [ ] Not started [ ] In progress [ ] Complete

---

### 13. Float16 Quantization Accuracy

**Task:** Verify Float16 quantized models maintain accuracy.

**Metrics:**

| Dataset | Float32 Baseline | Float16 Accuracy | Drop % | Status |
|---------|-----------------|------------------|--------|--------|
| STS benchmark (English) | 86.5 | 86.1 | <0.5% | [ ] Pass |
| MTEB benchmark | ? | ? | <1% | [ ] Pass |

**Expected Result:**
- [ ] Float16 accuracy drop <1%
- [ ] Speed improvement 1.5-2x vs. Float32

**Responsible:** ML/Data team  
**Status:** [ ] Not started [ ] In progress [ ] Complete

---

## Documentation & Release

### 14. Documentation Complete

**Task:** Ensure all documentation is updated for Phase 2.

- [ ] AOT deployment guide (docs/aot-deployment-guide.md)
- [ ] Quantization usage guide (docs/quantization-guide.md)
- [ ] Azure Functions sample (samples/AzureFunctionsSample/README.md)
- [ ] AWS Lambda sample (samples/LambdaSample/README.md)
- [ ] README.md updated with AOT & quantization examples
- [ ] API reference updated (Phase 2 section)
- [ ] Migration guide for PreferQuantized → PreferQuantization

**Responsible:** Technical writer / Architect  
**Status:** [ ] Not started [ ] In progress [ ] Complete

---

### 15. Security Audit Phase 2

**Task:** Conduct security review of AOT & quantization features.

**Checklist:**
- [ ] No new reflection vulnerabilities (reflection is locked down for AOT)
- [ ] Model loading path is secure (path traversal checks remain)
- [ ] Quantization model verification (hash checks still apply)
- [ ] No new credential exposure vectors
- [ ] ONNX Runtime native library validation
- [ ] Deployment security (Docker, Azure Functions)

**Expected Result:**
- [ ] Zero critical vulnerabilities found
- [ ] All Phase 2 features approved for release

**Responsible:** Security team (Ash)  
**Status:** [ ] Not started [ ] In progress [ ] Complete

---

### 16. Release Checklist

**Task:** Final preparation for Phase 2 release.

- [ ] All tests pass (314+ existing + new AOT/Quantization tests)
- [ ] No compiler warnings
- [ ] No analyzer warnings
- [ ] Code review complete
- [ ] Performance benchmarks documented
- [ ] Release notes written
- [ ] Version number bumped (minor: 1.5.0)
- [ ] NuGet package built and signed
- [ ] GitHub release created with changelog

**Responsible:** Release manager  
**Status:** [ ] Not started [ ] In progress [ ] Complete

---

## Rollback Plan

If critical AOT issues discovered:

1. **Do NOT ship Phase 2 with PublishAot=true requirement**
2. **Mark AOT as "experimental" in documentation**
3. **Investigate root cause:**
   - Is it ONNX Runtime incompatibility?
   - Is it dependency issue?
   - Is it usage pattern issue?
4. **Create GitHub issue with full details**
5. **Plan Phase 2b hot-fix**

---

## Sign-Off

**Release Checklist Complete:** [ ] Yes [ ] No

**Approved by:**
- [ ] Architect (Ripley)
- [ ] Team Lead
- [ ] QA Lead
- [ ] Security Lead (Ash)
- [ ] Release Manager

**Release Date:** ________________

---

**End of AOT Validation Checklist**
