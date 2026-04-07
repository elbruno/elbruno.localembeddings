# Decision: Add DirectML GPU Support to ElBruno.LocalEmbeddings.Harrier

**Date:** 2026-04-08
**Author:** Dallas (Core Dev)
**Branch:** `feature/harrier-gpu-directml`
**Status:** Implemented — pending coordinator review

---

## Context

`HarrierMultilingualSample` and `HarrierConsoleApp` always ran on CPU even on Windows machines
with capable GPUs. The root cause was three-fold:

1. `ElBruno.LocalEmbeddings.Harrier.csproj` referenced only `Microsoft.ML.OnnxRuntime` (CPU-only).
2. `HarrierEmbeddingsOptions` had no GPU/DirectML surface.
3. `HarrierOnnxEmbeddingModel.Load()` never registered any GPU execution provider.

## Decision

Add DirectML GPU acceleration behind a platform-conditional compile guard and an opt-in options flag.

## Changes Made

### 1. Conditional NuGet packages + preprocessor constant

```xml
<PropertyGroup Condition="'$(OS)' == 'Windows_NT'">
  <DefineConstants>$(DefineConstants);DIRECTML</DefineConstants>
</PropertyGroup>

<PackageReference Include="Microsoft.ML.OnnxRuntime.DirectML" Version="1.24.4"
  Condition="'$(OS)' == 'Windows_NT'" />
<PackageReference Include="Microsoft.ML.OnnxRuntime" Version="1.24.4"
  Condition="'$(OS)' != 'Windows_NT'" />
```

Rationale: `AppendExecutionProvider_DML` only exists in the DirectML package. The `#if DIRECTML`
guard in C# ensures the call is compiled out entirely on non-Windows targets, so the Linux/macOS
CPU-only build is never affected.

### 2. `HarrierEmbeddingsOptions` — two new properties

| Property | Type | Default | Notes |
|---|---|---|---|
| `UseDirectML` | `bool` | `false` | Enable DirectML GPU acceleration (Windows-only) |
| `DirectMLDeviceId` | `int` | `0` | GPU device index when DirectML is used |

Opt-in default (`false`) preserves backward compatibility — existing code continues to use CPU
without any change.

### 3. `HarrierOnnxEmbeddingModel.Load()` — extended signature

```csharp
public void Load(
    string modelPath,
    bool useParallelExecution = true,
    int? interOpNumThreads = null,
    int? intraOpNumThreads = null,
    bool useDirectML = false,       // NEW
    int directMLDeviceId = 0)       // NEW
```

Inside the try block, before `new InferenceSession(...)`:

```csharp
#if DIRECTML
if (useDirectML)
{
    sessionOptions.AppendExecutionProvider_DML(directMLDeviceId);
}
#endif
```

Exception filter broadened from `DllNotFoundException` to:

```csharp
catch (Exception ex) when (ex is DllNotFoundException or TypeInitializationException)
```

`TypeInitializationException` can be thrown by the DirectML runtime on machines without
DirectX 12 support, mirroring the pattern already used in the base library.

### 4. `HarrierEmbeddingGenerator` — pass-through to `Load()`

```csharp
_model.Load(
    modelPath,
    options.UseParallelExecution,
    options.InterOpNumThreads,
    options.IntraOpNumThreads,
    options.UseDirectML,
    options.DirectMLDeviceId);
```

### 5. Sample updates

Both `HarrierMultilingualSample` and `HarrierConsoleApp` now auto-detect Windows and set
`UseDirectML = true` automatically:

```csharp
bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
bool useGpu = isWindows;
var options = new HarrierEmbeddingsOptions { ..., UseDirectML = useGpu };
```

Platform and acceleration are printed to the console for visibility.

## Alternatives Considered

1. **Always enable DirectML on Windows unconditionally** — rejected because some machines may
   have broken GPU drivers. Opt-in default (`false`) lets users control this.
2. **Runtime reflection to call DML provider** — rejected; compile-time `#if` is cleaner and
   avoids reflection overhead.
3. **Separate `HarrierGpuEmbeddingGenerator` class** — rejected; adding parameters to the
   existing options is simpler and consistent with base library patterns.

## Risks and Mitigations

| Risk | Mitigation |
|---|---|
| DirectML unavailable at runtime (no DX12) | `TypeInitializationException` now caught and re-thrown with clear diagnostics |
| Linux/macOS builds broken by missing DML method | `#if DIRECTML` guard fully excludes the call on non-Windows |
| Performance regression on CPU path | `UseDirectML` defaults to `false`; CPU path is unchanged |
| Breaking change to `Load()` signature | All new parameters have defaults; existing callers compile without changes |
