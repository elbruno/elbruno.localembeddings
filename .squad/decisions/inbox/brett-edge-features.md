# NPU Fallback Telemetry and Native AOT Foundation

**By:** Brett (Edge/IoT Specialist)  
**Date:** 2026-04-04  
**Status:** Implemented

## Decision

Added OpenTelemetry-compatible diagnostics for NPU execution provider fallback scenarios and established Native AOT compatibility baseline for the core library.

## Implementation

### Feature 5.4: NPU Fallback Telemetry

**Diagnostics Sources:**
- `ElBruno.LocalEmbeddings.Npu` — DirectML execution provider events
- `ElBruno.LocalEmbeddings.Npu.Qualcomm` — QNN execution provider events

**Activity Tags:**
- `npu.execution_provider` / `qnn.execution_provider` — The provider being used (DirectML-NPU, DirectML-GPU, QNN, CPU)
- `npu.fallback` / `qnn.fallback` — Boolean indicating fallback occurred
- `npu.fallback_reason` / `qnn.fallback_reason` — Human-readable reason for fallback
- `npu.device_id`, `npu.device_description`, `npu.is_npu` — Device selection metadata

**Usage:**
```csharp
// Listen to NPU telemetry
using var listener = new ActivityListener
{
    ShouldListenTo = source => source.Name == "ElBruno.LocalEmbeddings.Npu",
    Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
    ActivityStarted = activity => Console.WriteLine($"NPU inference started: {activity.DisplayName}"),
    ActivityStopped = activity =>
    {
        if (activity.GetTagItem("npu.fallback") is true)
        {
            var reason = activity.GetTagItem("npu.fallback_reason");
            Console.WriteLine($"NPU fallback: {reason}");
        }
    }
};
ActivitySource.AddActivityListener(listener);
```

**Integration Points:**
- `NpuOnnxEmbeddingModel.Load()` — Records fallback when NPU hardware not detected
- `NpuOnnxEmbeddingModel.GenerateEmbeddings()` — Creates inference activities with device metadata
- `QualcommOnnxEmbeddingModel.Load()` — Records architecture mismatch (x64 → CPU fallback)
- `QualcommOnnxEmbeddingModel.CreateSession()` — Records QNN provider failure → CPU fallback

### Feature 2.1: Native AOT Annotations (Foundation)

**Project-Level Annotations:**
- `<IsTrimmable>true</IsTrimmable>` — Library is safe for trimming
- `<IsAotCompatible>true</IsAotCompatible>` — Library is compatible with Native AOT

**Code-Level Annotations:**
- `ServiceCollectionExtensions.AddLocalEmbeddings(IConfiguration)` — Uses existing `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]` attributes for configuration binding
- `OnnxEmbeddingModel` — Added XML documentation noting ONNX Runtime native library loading behavior

**AOT Compatibility Status:**
- ✅ Core library code is trimming-safe
- ✅ ONNX Runtime uses P/Invoke (AOT-compatible)
- ✅ No reflection-based type loading detected
- ⚠️ Configuration binding (`IConfiguration → LocalEmbeddingsOptions`) requires unreferenced code (documented)

## Rationale

### Why Telemetry?
1. **Production visibility** — Developers need to know when NPU hardware is unavailable without verbose logging
2. **Performance tracking** — OpenTelemetry traces can correlate inference latency with execution provider
3. **Debugging** — Fallback reasons help diagnose driver, architecture, or hardware detection issues
4. **Standard patterns** — ActivitySource is the .NET standard for instrumentation (OpenTelemetry-compatible)

### Why Native AOT Now?
1. **Edge deployment** — IoT devices and containers benefit from smaller binaries and faster startup
2. **Serverless readiness** — Native AOT enables sub-100ms cold starts (critical for future serverless scenarios)
3. **Early detection** — Marking the library as AOT-compatible now prevents breaking changes later
4. **Foundation work** — Establishes the baseline; future features must maintain compatibility

### Why Not Fully AOT-Compatible?
- Configuration binding (`IConfiguration → strongly-typed options`) inherently uses reflection in .NET
- The `[RequiresUnreferencedCode]` attribute documents this limitation
- Direct configuration is already AOT-safe: `new LocalEmbeddingsOptions { ... }`
- This is acceptable — most edge deployments use hardcoded options

## Impact

**Developers:**
- Can monitor NPU fallback in production via OpenTelemetry
- Can deploy to Native AOT targets (with configuration binding caveat)
- Can diagnose NPU detection issues via telemetry tags

**Library Maintainers:**
- Must maintain AOT compatibility going forward
- New features using reflection must be annotated with `[RequiresUnreferencedCode]`
- Telemetry tags are now part of the public API surface (semantic versioning applies)

## Testing

**Build Validation:**
```bash
dotnet build src/ElBruno.LocalEmbeddings/ElBruno.LocalEmbeddings.csproj /p:PublishTrimmed=true
# Result: No trimming warnings
```

**NPU Telemetry:**
- Verified fallback events fire when NPU hardware not detected
- Verified inference activities created with correct execution provider tags
- QNN architecture mismatch (x64 → ARM64) fallback validated

## Future Work

1. **Batch telemetry** — Add batch size, token count, and throughput metrics to inference activities
2. **Native AOT sample** — Create a Native AOT-published console app sample for edge deployment
3. **FP16 precision telemetry** — Track when FP16 modes are used (roadmap 5.3)
4. **WASM deployment** — Test library in Blazor WASM with Native AOT (roadmap 3.2)

## Related

- Team roadmap: Priority 2.1 (Native AOT), 5.4 (NPU Telemetry)
- Squad charter: Brett owns edge/IoT optimization
- Telemetry follows .NET diagnostic standards: [Activity User Guide](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs)
