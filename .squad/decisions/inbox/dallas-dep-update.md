# Package Dependency Update Strategy — April 2026

**By:** Dallas (Core Dev)  
**Date:** 2026-02-13  
**Status:** Implemented

## Decision

Establish a systematic approach for updating NuGet package dependencies across the solution, prioritizing stability while staying current with the .NET ecosystem.

## Context

Updated all NuGet packages to their latest stable versions (April 2026). The update process revealed important patterns about package compatibility and breaking changes that should guide future updates.

## Key Findings

### 1. Microsoft.AI.Foundry.Local Breaking Changes

**Issue:** Version 0.9.0 introduces breaking API changes — the `StartModelAsync` method was removed, breaking the RagFoundryLocal sample.

**Decision:** Keep version 0.1.0 for this package until the sample can be refactored to use the new API.

**Lesson:** Preview packages (`Microsoft.AI.*`, `Microsoft.Extensions.AI.*`) may introduce breaking changes even in minor version bumps. Always verify sample compatibility before updating these packages.

### 2. Intel OpenVINO Versioning Isolation

**Issue:** `Intel.ML.OnnxRuntime.OpenVino` uses independent versioning (1.24.1) separate from Microsoft's OnnxRuntime packages (1.24.4) because it ships its own standalone runtime DLL.

**Decision:** Do not attempt to "align" Intel ORT versions with Microsoft ORT versions — they are intentionally isolated to avoid native DLL conflicts.

**Rationale:** The Npu.Intel project is a standalone library that doesn't reference the base ElBruno.LocalEmbeddings project precisely to avoid `onnxruntime.dll` version conflicts (one has OpenVINO EP compiled in, the other doesn't).

### 3. Test Package Major Version Jumps

Several test packages had major version increases:
- `coverlet.collector`: 6.0.4 → 8.0.1 (2 major versions)
- `Microsoft.NET.Test.Sdk`: 17.14.1 → 18.3.0 (1 major version)

**Result:** All 138 tests across net8.0 and net10.0 passed without modification — these packages maintain strong backward compatibility.

### 4. Multi-Target Build Verification

All projects multi-target `net8.0` and `net10.0`. Updated packages must work across both frameworks.

**Verification Process:**
1. `dotnet restore` — ensure all packages resolve
2. `dotnet clean; dotnet build` — verify compilation across all targets
3. `dotnet test --no-build` — run all tests for both frameworks

## Update Workflow (for future package updates)

```bash
# 1. Identify outdated packages
dotnet list package --outdated

# 2. Update .csproj files (use exact versions from NuGet, not wildcards)

# 3. Clean and restore
dotnet clean
dotnet restore

# 4. Build and test
dotnet build
dotnet test

# 5. For preview packages, verify sample compatibility manually
# (especially Microsoft.AI.*, Microsoft.Extensions.AI.Ollama, etc.)
```

## Package Categories and Update Priorities

### Critical (update promptly for security/performance):
- `Microsoft.ML.OnnxRuntime*` (core inference engine)
- `System.Numerics.Tensors` (SIMD operations)
- `Microsoft.Extensions.*` (framework integration)

### Important (update regularly):
- `Microsoft.Extensions.AI.*` (abstractions layer)
- Test packages (`xunit`, `Microsoft.NET.Test.Sdk`, etc.)
- `ElBruno.HuggingFace.Downloader` (our own dependency)

### Cautious (verify before updating):
- `Microsoft.AI.Foundry.Local` (preview, breaking changes possible)
- `Microsoft.Extensions.AI.Ollama` (preview)
- `Intel.ML.OnnxRuntime.OpenVino` (independent versioning)

## Impact

- **Positive:** Solution now uses latest stable packages with bug fixes, performance improvements, and new features from the .NET 10 ecosystem
- **Test Coverage:** All 138 tests passing confirms backward compatibility maintained
- **Breaking Changes:** One package (Foundry.Local) identified with breaking changes and appropriately handled

## Recommendation for Team

When updating packages:
1. Use `dotnet list package --outdated` to identify candidates
2. Check NuGet release notes for breaking changes (especially preview packages)
3. Update in batches by category (core → test → samples)
4. Always run full test suite after updates
5. Document any packages kept at older versions with rationale
