# Brett — History

## Project Context

- **Project:** ElBruno.LocalEmbeddings — .NET library for local embedding generation using ONNX Runtime and Microsoft.Extensions.AI
- **Owner:** Bruno Capuano
- **Stack:** .NET 10, C#, Microsoft.Extensions.AI, ONNX Runtime, HuggingFace models
- **Multi-targets:** net8.0;net10.0 for libraries, net10.0 for samples
- **Key focus areas:** ARM64 (Raspberry Pi), Native AOT, WebAssembly, model quantization, edge deployment

## Key Files

- `src/ElBruno.LocalEmbeddings/` — Core library
- `samples/RaspberryPiTiny/` — Existing Raspberry Pi sample
- `docs/roadmap.md` — Improvement roadmap (Priority 2.1 Native AOT, 3.2 Blazor WASM, 3.5 ARM64 optimization, 5.2-5.5)

## Learnings

### 2026-04-04: NPU Fallback Telemetry and Native AOT Foundation

**Feature 5.4: NPU Fallback Telemetry**
- Created `NpuDiagnostics.cs` with OpenTelemetry-compatible `ActivitySource` ("ElBruno.LocalEmbeddings.Npu") for DirectML execution provider events
- Created `QualcommNpuDiagnostics.cs` with dedicated `ActivitySource` ("ElBruno.LocalEmbeddings.Npu.Qualcomm") for QNN-specific events
- Integrated telemetry into `NpuOnnxEmbeddingModel` and `QualcommOnnxEmbeddingModel`:
  - `StartInference()` creates activities with execution provider tags
  - `RecordFallback()` emits events when NPU → CPU fallback happens (with reason)
  - `RecordDeviceSelection()` logs device ID, description, and NPU detection status
- Added `System.Diagnostics.DiagnosticSource 10.0.5` package to both NPU projects
- Developers can now use OpenTelemetry listeners to track NPU availability and performance in production

**Feature 2.1: Native AOT Annotations (Foundation)**
- Added `<IsTrimmable>true</IsTrimmable>` and `<IsAotCompatible>true</IsAotCompatible>` to `ElBruno.LocalEmbeddings.csproj`
- Added `using System.Diagnostics.CodeAnalysis` to `ServiceCollectionExtensions.cs` to enable existing `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]` attributes on configuration binding methods
- Documented ONNX Runtime native library loading limitations in `OnnxEmbeddingModel.cs` XML comments
- Fixed `OpenTelemetryEmbeddingMiddleware.cs` to use `GetService<EmbeddingGeneratorMetadata>()` instead of direct `.Metadata` property access for AOT compatibility
- Build validated with `/p:PublishTrimmed=true` — no trimming warnings
- **Key finding:** ONNX Runtime uses P/Invoke for native library loading, which is AOT-compatible. No reflection-based model loading detected in core library

**Technical Details**
- ActivitySource naming: hierarchical pattern (`ElBruno.LocalEmbeddings.Npu`, `ElBruno.LocalEmbeddings.Npu.Qualcomm`)
- Activity tags use semantic naming: `npu.execution_provider`, `npu.fallback_reason`, `qnn.execution_provider`
- Fallback events include both activity tags and discrete events for listeners without full tracing
- AOT annotations apply to library code only — ONNX models themselves are runtime-loaded (expected behavior)

**Files Modified**
- `src/ElBruno.LocalEmbeddings.Npu/NpuDiagnostics.cs` (new)
- `src/ElBruno.LocalEmbeddings.Npu/NpuOnnxEmbeddingModel.cs` (telemetry integration)
- `src/ElBruno.LocalEmbeddings.Npu.Qualcomm/QualcommNpuDiagnostics.cs` (new)
- `src/ElBruno.LocalEmbeddings.Npu.Qualcomm/QualcommOnnxEmbeddingModel.cs` (telemetry integration)
- `src/ElBruno.LocalEmbeddings/ElBruno.LocalEmbeddings.csproj` (AOT properties)
- `src/ElBruno.LocalEmbeddings/OnnxEmbeddingModel.cs` (AOT documentation)
- `src/ElBruno.LocalEmbeddings/Extensions/ServiceCollectionExtensions.cs` (using statement)
- `src/ElBruno.LocalEmbeddings/Middleware/OpenTelemetryEmbeddingMiddleware.cs` (GetService fix)

**Next Steps**
- Consider telemetry for batch size, inference duration, and memory pressure (future roadmap item)
- Test Native AOT deployment on edge devices (Raspberry Pi, IoT Core)
- Investigate FP16 precision modes for NPU optimization (roadmap 5.3)

