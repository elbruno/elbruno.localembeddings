# Phase 2: Native AOT + Quantization Architecture Design
**Prepared by:** Ripley (Lead Architect)  
**Date:** 2026-05-19  
**Status:** Architecture Design (Ready for Implementation)

---

## Executive Summary

Phase 2 architecture enables **serverless deployment** (Azure Functions, AWS Lambda, Google Cloud Functions) via Native AOT (Ahead-of-Time) compilation and exposes **quantization controls** to users for fine-grained speed/accuracy tradeoffs. This design unblocks production deployment while maintaining full backward compatibility.

**Key Outcomes:**
- Native AOT readiness with <5% performance overhead
- Minimal API surface for quantization (no breaking changes)
- Graceful degradation for quantized model variants
- Clear deployment strategy for serverless environments
- Validation checklist for AOT compliance

---

## Phase 1 Context & Achievements

### SIMD Optimization ✅
- **Status:** Complete and production-ready
- **Performance:** 47-96 nanoseconds per vector (384-768 dim)
- **Impact:** 2-3x speedup over naive implementations

### Streaming API ✅
- **Status:** Architecture + skeleton implementation complete
- **Memory:** O(buffer_size) = constant bounded memory
- **Throughput:** 4,300+ corpus evaluations/ms

### Security Audit ✅
- **Status:** Zero critical vulnerabilities
- **Coverage:** Input validation, path traversal, model integrity, credentials
- **Recommendation:** Safe to ship Phase 1B

---

## Part 1: Native AOT Readiness

### 1.1 Current AOT State

**Good News:**
```csharp
// Already AOT-compatible:
- Zero reflection in embedding inference path
- No dynamic code generation
- No DynamicMethod or Expression.Compile()
- No System.Reflection.Emit usage
- Type metadata preserved via IsAotCompatible=true in csproj
```

**Current csproj Settings:**
```xml
<IsTrimmable>true</IsTrimmable>          <!-- ✅ Enabled -->
<IsAotCompatible>true</IsAotCompatible>  <!-- ✅ Enabled -->
```

### 1.2 AOT-Incompatible Patterns Identified

#### Pattern 1: Configuration Binding (ServiceCollectionExtensions.cs:194-204)
```csharp
// PROBLEMATIC - requires reflection for property binding
[RequiresUnreferencedCode("...")]
[RequiresDynamicCode("...")]
public static IServiceCollection AddLocalEmbeddings(
    this IServiceCollection services,
    IConfiguration configuration)
{
    services.AddOptions<LocalEmbeddingsOptions>()
        .Bind(configuration);  // ← Reflection-based binding
    return services.AddLocalEmbeddingsCore();
}
```

**Severity:** MEDIUM (used in ASP.NET/config-driven scenarios)  
**Solution:** This overload is already marked with [RequiresUnreferencedCode] — it's explicitly documented as AOT-incompatible. Users deploying to AOT must use alternatives:
- `AddLocalEmbeddings(LocalEmbeddingsOptions options)` — direct instance ✅
- `AddLocalEmbeddings(Action<LocalEmbeddingsOptions>)` — delegate ✅

#### Pattern 2: ONNX Runtime Native Dependencies
```csharp
// Microsoft.ML.OnnxRuntime loads native dlls at runtime
// onnxruntime.dll/.so/.dylib must be available
```

**Severity:** MEDIUM (fundamental to ONNX inference)  
**Solution:** Document native library deployment requirements for AOT scenarios (see Deployment Strategy section).

#### Pattern 3: Tokenizer (Microsoft.ML.Tokenizers)
```csharp
// Uses BertTokenizer.Create(stream) — pure managed code
// ✅ AOT-safe (file I/O is resolved at compile-time)
```

**Severity:** LOW (no issues identified)

#### Pattern 4: File I/O Paths
```csharp
// Model discovery logic uses Directory.Exists, File.Exists
// ✅ AOT-compatible (not reflection-based)
var modelPath = ResolveModelPath(modelDirectory, options.PreferQuantized);
```

**Severity:** LOW (safe)

### 1.3 Trimming Metadata Strategy

**Goal:** Ensure only necessary types are included in trimmed/AOT builds.

#### Add TrimmerRootAssembly Directives
**File:** `src/ElBruno.LocalEmbeddings/ElBruno.LocalEmbeddings.csproj`

```xml
<ItemGroup>
    <TrimmerRootAssembly Include="ElBruno.LocalEmbeddings" />
    <!-- Service registration entry points must be reachable -->
</ItemGroup>
```

#### Enable Runtime Trimming Analysis
**File:** `src/ElBruno.LocalEmbeddings/ElBruno.LocalEmbeddings.csproj`

```xml
<PropertyGroup>
    <PublishTrimmed>true</PublishTrimmed>
    <PublishTrimmedWithDefaultRuntimePackage>true</PublishTrimmedWithDefaultRuntimePackage>
    <TrimMode>partial</TrimMode>
    <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
</PropertyGroup>
```

#### Trimming Suppressions (Minimal)
**File:** `src/ElBruno.LocalEmbeddings/TrimmerRoot.cs` (new file)

```csharp
using System.Diagnostics.CodeAnalysis;

namespace ElBruno.LocalEmbeddings;

/// <summary>
/// Trimmer root preserving entry points and reflection-free public API.
/// </summary>
[Trimmer.PublicAPI]
internal static class TrimmerRoot
{
    /// <summary>
    /// Ensures LocalEmbeddingGenerator is reachable for AOT scenarios.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | 
                                DynamicallyAccessedMemberTypes.PublicConstructors)]
    public static void EnsureLocalEmbeddingGeneratorMetadata()
    {
        // This method is never called; exists only to preserve metadata.
        _ = typeof(LocalEmbeddingGenerator);
    }

    /// <summary>
    /// Ensures ServiceCollectionExtensions are reachable for DI scenarios.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
    public static void EnsureServiceExtensionsMetadata()
    {
        _ = typeof(Extensions.ServiceCollectionExtensions);
    }
}
```

### 1.4 AOT Test Harness

**File:** `tests/ElBruno.LocalEmbeddings.Tests/AotCompatibilityTests.cs` (new file)

```csharp
using Xunit;
using Microsoft.Extensions.DependencyInjection;

namespace ElBruno.LocalEmbeddings.Tests;

/// <summary>
/// Tests for Native AOT compatibility.
/// </summary>
[Trait("Category", "AotCompatibility")]
public sealed class AotCompatibilityTests
{
    [Fact]
    public void DirectInstantiation_Works()
    {
        // No reflection required
        var options = new LocalEmbeddingsOptions 
        { 
            ModelPath = "test_model",
            PreferQuantized = true
        };
        
        // This should work in AOT (no reflection in constructor)
        var generator = new LocalEmbeddingGenerator(options);
        Assert.NotNull(generator);
    }

    [Fact]
    public void DependencyInjectionWithDelegate_Works()
    {
        var services = new ServiceCollection();
        
        // AOT-compatible overload (no reflection in Bind())
        services.AddLocalEmbeddings(options =>
        {
            options.ModelName = "test-model";
            options.PreferQuantized = true;
        });
        
        var provider = services.BuildServiceProvider();
        var generator = provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        Assert.NotNull(generator);
    }

    [Fact]
    public void DependencyInjectionWithPrebuiltOptions_Works()
    {
        var services = new ServiceCollection();
        
        var options = new LocalEmbeddingsOptions 
        { 
            ModelPath = "test_model",
            PreferQuantized = true
        };
        
        services.AddLocalEmbeddings(options);
        
        var provider = services.BuildServiceProvider();
        var generator = provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        Assert.NotNull(generator);
    }

    [Fact]
    public void ConfigurationBinding_ThrowsRequiresUnreferencedCodeWarning()
    {
        // This overload is intentionally NOT AOT-compatible
        // Verify the warning attributes are present
        var method = typeof(Extensions.ServiceCollectionExtensions)
            .GetMethod(nameof(Extensions.ServiceCollectionExtensions.AddLocalEmbeddings),
                       [typeof(IServiceCollection), typeof(IConfiguration)]);
        
        Assert.NotNull(method);
        
        var attrs = method?.GetCustomAttributes()
            .Where(a => a.GetType().Name.Contains("RequiresUnreferencedCode") ||
                        a.GetType().Name.Contains("RequiresDynamicCode"))
            .ToList();
        
        Assert.NotEmpty(attrs ?? []);
    }
}
```

---

## Part 2: Quantization API Surface

### 2.1 Design Goals

1. **Minimal API Surface** — No new public interfaces, only options
2. **No Breaking Changes** — Fully backward compatible
3. **Graceful Degradation** — If quantized variant not found, use full-precision
4. **User Control** — Explicit quantization preference at registration time
5. **Performance** — Quantized models 2-4x faster, minimal accuracy loss (<2%)

### 2.2 Quantization Options

**Extend LocalEmbeddingsOptions:**

```csharp
namespace ElBruno.LocalEmbeddings.Options;

/// <summary>
/// Quantization format for embedding models.
/// </summary>
public enum QuantizationFormat
{
    /// <summary>
    /// Full precision (32-bit float). Default, no quantization.
    /// </summary>
    Float32,

    /// <summary>
    /// INT8 quantization (8-bit signed integer).
    /// Reduces model size ~4x, speed ~2-3x, minimal accuracy loss (~1-2%).
    /// </summary>
    Int8,

    /// <summary>
    /// Float16 quantization (16-bit half-precision).
    /// Reduces model size ~2x, speed ~1.5-2x, minimal accuracy loss (<1%).
    /// </summary>
    Float16
}

/// <summary>
/// Configuration options for LocalEmbeddingGenerator with quantization support.
/// </summary>
public sealed class LocalEmbeddingsOptions
{
    // ... existing properties ...

    /// <summary>
    /// Gets or sets the quantization format to use when loading models.
    /// Default is <see cref="QuantizationFormat.Float32"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When set to <see cref="QuantizationFormat.Int8"/> or <see cref="QuantizationFormat.Float16"/>,
    /// the model loader will search for quantized model files:
    /// - INT8: <c>model_quantized.onnx</c>, <c>model_int8.onnx</c>
    /// - Float16: <c>model_fp16.onnx</c>, <c>model_float16.onnx</c>
    /// </para>
    /// <para>
    /// If the quantized variant is not found, the loader automatically falls back to <c>model.onnx</c>
    /// (full precision).
    /// </para>
    /// <para>
    /// Quantized models must be pre-converted using ONNX quantization tools (e.g., onnxruntime-tools).
    /// </para>
    /// </remarks>
    public QuantizationFormat PreferQuantization { get; set; } = QuantizationFormat.Float32;
}
```

**Backward Compatibility Note:**
The existing `PreferQuantized` (bool) property remains for backward compatibility but is deprecated:

```csharp
/// <summary>
/// Gets or sets whether to prefer a quantized model variant.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Deprecated:</strong> Use <see cref="PreferQuantization"/> instead for finer control.
/// </para>
/// <para>
/// When <c>true</c>, equivalent to <see cref="PreferQuantization"/> = <see cref="QuantizationFormat.Int8"/>.
/// </para>
/// </remarks>
[Obsolete("Use PreferQuantization instead.", false)]
public bool PreferQuantized { get; set; } = false;
```

### 2.3 Quantization Metadata Schema

**Quantized Model Registry** — File: `docs/quantization-model-registry.md`

```markdown
# Quantization Model Registry

Tracks which HuggingFace models have quantized variants available.

## Format

```json
{
  "models": [
    {
      "name": "sentence-transformers/all-MiniLM-L6-v2",
      "dimension": 384,
      "variants": [
        {
          "format": "float32",
          "file": "model.onnx",
          "size_mb": 134,
          "hash": "abc123...",
          "accuracy_baseline": true
        },
        {
          "format": "int8",
          "file": "model_quantized.onnx",
          "size_mb": 33,
          "hash": "def456...",
          "accuracy_drop_percent": 1.2,
          "speed_improvement_percent": 250
        },
        {
          "format": "float16",
          "file": "model_fp16.onnx",
          "size_mb": 67,
          "hash": "ghi789...",
          "accuracy_drop_percent": 0.5,
          "speed_improvement_percent": 180
        }
      ]
    }
  ]
}
```

### 2.4 Quantization API Implementation Strategy

**File:** `src/ElBruno.LocalEmbeddings/LocalEmbeddingGenerator.cs`

Key changes:
1. Detect `PreferQuantization` setting during model loading
2. Search for quantized model file names in priority order
3. Fall back to full-precision if quantized not found
4. Log quantization status for observability

```csharp
private static string ResolveModelPath(string modelDirectory, QuantizationFormat preferQuantization)
{
    // Define search patterns by quantization format
    var candidateFiles = preferQuantization switch
    {
        QuantizationFormat.Int8 => 
            new[] { "model_quantized.onnx", "model_int8.onnx", "model.onnx" },
        QuantizationFormat.Float16 => 
            new[] { "model_fp16.onnx", "model_float16.onnx", "model.onnx" },
        _ => new[] { "model.onnx" }
    };

    foreach (var filename in candidateFiles)
    {
        var path = Path.Combine(modelDirectory, filename);
        if (File.Exists(path))
        {
            return path;
        }
    }

    throw new FileNotFoundException(
        $"No model files found in {modelDirectory}. Expected: {string.Join(", ", candidateFiles)}");
}
```

### 2.5 Usage Example

```csharp
// Example 1: Enable INT8 quantization (2-3x faster, 4x smaller)
var services = new ServiceCollection();
services.AddLocalEmbeddings(options =>
{
    options.ModelName = "sentence-transformers/all-MiniLM-L6-v2";
    options.PreferQuantization = QuantizationFormat.Int8;
    // Falls back to full-precision automatically if quantized not available
});

// Example 2: Use pre-built options with Float16
var options = new LocalEmbeddingsOptions
{
    ModelPath = "/models/my-model",
    PreferQuantization = QuantizationFormat.Float16
};
var generator = new LocalEmbeddingGenerator(options);

// Example 3: Full precision (no quantization)
services.AddLocalEmbeddings(options =>
{
    options.PreferQuantization = QuantizationFormat.Float32;  // Explicit
});
```

---

## Part 3: Deployment Strategy for Serverless

### 3.1 Native AOT Deployment Workflow

#### Step 1: AOT-Safe Code Paths
```bash
# Use only these DI patterns:
# ✅ AddLocalEmbeddings(options => { ... })
# ✅ AddLocalEmbeddings(LocalEmbeddingsOptions)
# ❌ AddLocalEmbeddings(IConfiguration) — requires reflection
```

#### Step 2: Publish for AOT
```bash
dotnet publish -c Release -f net8.0 \
    -p:PublishAot=true \
    -p:SelfContained=true
```

#### Step 3: Deploy Native Executable
```
MyApp.exe (Linux: ./MyApp)  ← Single executable, ~50 MB
onnxruntime.dll/.so         ← Required native dependency
model.onnx                  ← Quantized model (4-30 MB)
```

### 3.2 Azure Functions Deployment

**Azure Functions Template (net8.0 isolated):**

```csharp
using Microsoft.Azure.Functions.Worker;
using ElBruno.LocalEmbeddings;

public class EmbeddingFunction
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;

    public EmbeddingFunction(
        IEmbeddingGenerator<string, Embedding<float>> generator)
    {
        _generator = generator;
    }

    [Function("GenerateEmbedding")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var texts = JsonSerializer.Deserialize<string[]>(requestBody);

        var embeddings = await _generator.GenerateAsync(texts!);
        
        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteAsJsonAsync(embeddings);
        return response;
    }
}
```

**Program.cs for AOT:**
```csharp
var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        // ✅ AOT-safe: delegate-based configuration
        services.AddLocalEmbeddings(options =>
        {
            options.ModelPath = Environment.GetEnvironmentVariable("MODEL_PATH");
            options.PreferQuantization = QuantizationFormat.Int8;
        });
    })
    .Build();

host.Run();
```

**Deployment:**
```bash
# 1. Publish with AOT
dotnet publish -c Release -f net8.0 -p:PublishAot=true

# 2. Upload to Azure Functions
func azure functionapp publish MyFunctionApp \
    --build remote --build-native

# 3. Configure environment variable
# MODEL_PATH=/home/site/wwwroot/model_quantized.onnx
```

### 3.3 AWS Lambda / Google Cloud Deployment

Similar pattern: use quantized models + AOT compilation to meet cold-start latency budgets.

---

## Part 4: Validation Checklist

### AOT Compliance Checklist

- [ ] **Build Target:** Verify `<IsAotCompatible>true</IsAotCompatible>` in csproj
- [ ] **Trimming:** Enable `<PublishTrimmed>true</PublishTrimmed>`
- [ ] **Analysis:** Run trimmer analyzer and resolve warnings:
  ```bash
  dotnet publish -p:PublishAot=true --self-contained
  ```
- [ ] **Reflection:** Scan codebase for:
  - [ ] `Type.GetType("...")`
  - [ ] `MethodInfo.Invoke()`
  - [ ] `Activator.CreateInstance()`
  - [ ] `Reflection.Emit`
  - Result: ✅ None found (reflection-free inference path)
- [ ] **DynamicCode:** Verify no `Expression.Compile()` or `DynamicMethod`
  - Result: ✅ Only used in options binding (already marked as non-AOT)
- [ ] **Unsafe Blocks:** Verify no unsafe code in public API
  - Result: ✅ No unsafe blocks
- [ ] **Native Dependencies:** Document ONNX Runtime deployment
  - [ ] onnxruntime.dll/.so/.dylib included in deployment
  - [ ] Architecture matches (x64, ARM64, etc.)
- [ ] **Tests:** Run AotCompatibilityTests
  - [ ] DirectInstantiation_Works
  - [ ] DependencyInjectionWithDelegate_Works
  - [ ] DependencyInjectionWithPrebuiltOptions_Works
  - [ ] ConfigurationBinding_ThrowsWarning

### Quantization Compliance Checklist

- [ ] **API Surface:** Verify no breaking changes
  - [ ] `LocalEmbeddingsOptions.PreferQuantization` added (new)
  - [ ] `PreferQuantized` still works (deprecated but functional)
- [ ] **Model Resolution:** Test fallback chain
  - [ ] `model_quantized.onnx` loaded when available
  - [ ] Falls back to `model.onnx` gracefully
- [ ] **Accuracy Testing:** Verify quantized models
  - [ ] INT8: <2% accuracy drop
  - [ ] Float16: <1% accuracy drop
- [ ] **Performance:** Benchmark quantized models
  - [ ] INT8: 2-3x faster inference
  - [ ] Float16: 1.5-2x faster inference
- [ ] **Documentation:** Update README & guides
  - [ ] Usage examples for quantization
  - [ ] Deployment instructions for AOT

---

## Part 5: Effort & Timeline Estimate

### Phase 2 Implementation Roadmap

| Task | Effort | Dependencies | Notes |
|------|--------|--------------|-------|
| **AOT Configuration** | 2 days | None | Add trimming metadata, test harness |
| **Quantization Options** | 2 days | None | Extend LocalEmbeddingsOptions, update model resolver |
| **Quantization Testing** | 3 days | Quantization Options | Int8/Float16 accuracy & perf benchmarks |
| **AOT Testing** | 2 days | AOT Configuration | Run PublishAot=true, resolve warnings |
| **Azure Functions Sample** | 2 days | Quantization Options + AOT Testing | Create template, test cold-start |
| **Documentation** | 3 days | All above | Architecture guide, deployment guide, API docs |
| **Code Review & Polish** | 2 days | All above | Fix issues, finalize APIs |
| **Security Audit Phase 2** | 2 days | All above | Verify AOT-specific security concerns |
| **Total** | **18 days** | — | ~3 weeks for full implementation |

### Phase 2 Deliverables

1. ✅ **Architecture Design Doc** (this document)
2. ✅ **Quantization Metadata Schema** (quantization-model-registry.md)
3. ✅ **AOT Validation Checklist** (aot-validation-checklist.md)
4. Native AOT support in csproj (trimming metadata)
5. AOT test harness (AotCompatibilityTests.cs)
6. Quantization API (QuantizationFormat enum + options)
7. Azure Functions / Lambda deployment templates
8. Performance benchmarks for quantized models
9. Updated README with AOT & quantization examples
10. Phase 2 implementation guide for team

---

## Part 6: Potential Risks & Mitigation

### Risk 1: ONNX Runtime Availability
**Risk:** ONNX Runtime native libraries not included in AOT deployments.  
**Severity:** HIGH  
**Mitigation:**
- Document native library deployment requirements
- Provide publishing helper script
- Add pre-deployment validation test

### Risk 2: Quantized Model Quality
**Risk:** INT8 quantization causes accuracy degradation >2%.  
**Severity:** MEDIUM  
**Mitigation:**
- Benchmark on standard datasets (STS, MTEB)
- Document accuracy-speed tradeoffs per model
- Provide guidelines for choosing quantization format

### Risk 3: Backward Compatibility
**Risk:** Existing code breaks with new `PreferQuantization` property.  
**Severity:** LOW  
**Mitigation:**
- Keep `PreferQuantized` property functional (deprecated)
- Provide migration guide
- Use semantic versioning (minor version bump)

### Risk 4: Cold-Start Latency
**Risk:** Model loading takes too long in serverless.  
**Severity:** MEDIUM  
**Mitigation:**
- Document model pre-warming strategies
- Provide Lambda/Functions layer templates
- Benchmark cold-start with quantized models

---

## Part 7: Success Criteria

✅ **Phase 2 is complete when:**

1. Native AOT builds successfully on .NET 8 & 10
   ```bash
   dotnet publish -p:PublishAot=true
   ```
   - Single executable generated
   - <5% performance overhead vs non-AOT

2. Quantization API works end-to-end
   - `PreferQuantization` option set correctly
   - Model fallback chain works
   - INT8 & Float16 variants load correctly

3. Azure Functions deployment succeeds
   - Function publishes as AOT-native executable
   - Cold-start latency <2 seconds
   - Embedding generation works

4. All tests pass
   - 314+ existing tests still pass
   - AotCompatibilityTests pass
   - QuantizationTests pass (new)

5. Documentation complete
   - AOT deployment guide
   - Quantization usage guide
   - Serverless samples (Azure/AWS/GCP)

6. Zero security regressions
   - Security audit Phase 2 complete
   - No new CVEs introduced

---

## Part 8: References

- **ONNX Runtime AOT:** https://github.com/Microsoft/onnxruntime/wiki/Native-AOT
- **Microsoft.ML.Tokenizers:** AOT-compatible, pure managed code
- **.NET AOT Deployment:** https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/
- **Azure Functions Native AOT:** https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide
- **ONNX Quantization:** https://github.com/microsoft/onnxruntime-tools
- **Phase 1 Deliverables:** SIMD_OPTIMIZATION_REPORT.md, STREAMING_API_DELIVERY.md, SECURITY_AUDIT_PHASE1.md

---

## Next Steps (For Implementation Team)

1. **Review & Approve** this architecture design
2. **Create GitHub Epic** for Phase 2 with 8 implementation tasks
3. **Assign Tasks** to team members
4. **Begin Implementation** with AOT configuration (lowest risk first)
5. **Parallel Track:** Quantization API while AOT testing runs
6. **Integrate** Azure Functions sample after both features complete
7. **Security Audit** Phase 2 deliverables
8. **Release** Phase 2 in next version with release notes

---

**Ripley — Lead Architect**  
*Architecting for serverless scale.*

---

**End of Design Document**
