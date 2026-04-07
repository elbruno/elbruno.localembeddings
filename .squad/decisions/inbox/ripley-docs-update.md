# Documentation Update — Harrier Package

**By:** Ripley (Lead / Architect)  
**Date:** 2026-02-28  
**Status:** Complete  

---

## Summary

Updated all 6 documentation items identified in the Harrier architecture review (`.squad/decisions/inbox/ripley-harrier-arch-review.md`). All changes follow established conventions and the solution builds successfully.

---

## Changes Made

### 1. README.md — Harrier Visibility

**File:** `README.md`

**Changes:**
- ✅ Added Harrier to Features section with 🦅 icon and key details (270M, 640-dim, 94+ languages, instruction-tuned)
- ✅ Updated Installation section to add `dotnet add package ElBruno.LocalEmbeddings.Harrier` command
- ✅ Added Quick Start example #5 showing Harrier usage (640-dim output)
- ✅ Updated Documentation table to include `[Harrier Integration](docs/harrier-integration.md)`
- ✅ Updated Samples table to include `[HarrierConsoleApp](samples/HarrierConsoleApp/)`

**Impact:** Harrier is now fully visible in the main project README, at feature parity with the base library presentation.

---

### 2. docs/changelog.md — Harrier Release Notes

**File:** `docs/changelog.md`

**Changes:**
- ✅ Added new `[Unreleased] - 2026-02-28` section (moved old 2026-02-14 to secondary entry)
- ✅ **Added:** `ElBruno.LocalEmbeddings.Harrier` package with full feature list
- ✅ **Added:** Shared multilingual test suite (`SharedModelTests`) covering 10 languages
- ✅ **Added:** HarrierConsoleApp sample
- ✅ **Added:** `docs/harrier-integration.md` guide
- ✅ **Added:** Harrier to CI/CD (NuGet publishing)
- ✅ **Added:** Security findings (SHA-256 sidecar, concurrent download serialization, file size guards)
- ✅ **Added:** Performance optimizations (CountTokens, allocation patterns, SentencePiece normalization)
- ✅ **Fixed:** HarrierTokenizer maxLength=1 bug, .onnx_data companion file verification

**Impact:** Changelog now reflects the entire Harrier implementation and all security/performance audit work.

---

### 3. docs/harrier-integration.md — Migration Guide

**File:** `docs/harrier-integration.md`

**Changes:**
- ✅ Added `## Migrating from MiniLM to Harrier` section with 5 subsections:
  1. **Vector Store Re-indexing** — Warns about 384→640 dimension change, re-indexing requirement, and example code
  2. **DI Swap** — Shows before/after code for replacing `AddLocalEmbeddings()` with `AddHarrierEmbeddings()`
  3. **Instruction Prefix Setup** — Documents instruction tuning, provides task examples, warns about prefix-on-queries-only
  4. **Model Size Considerations** — Compares MiniLM vs Harrier sizes (90 MB → 500 MB FP32 / 270 MB quantized), variants table
  5. **MaxSequenceLength Optimization** — Shows how to reduce from 8192 to 512 for memory savings
- ✅ Added Summary Checklist with 6 items

**Impact:** Users migrating from MiniLM have a step-by-step guide addressing the key breaking change (dimensions) and all configuration differences.

---

### 4. samples/README.md — Missing Samples

**File:** `samples/README.md`

**Changes:**
- ✅ Updated header count: "Eight sample projects" → "Sixteen sample projects"
- ✅ Updated Overview table with ALL samples (was 8, now includes 16):
  - Added `HarrierConsoleApp` after `ConsoleApp`
  - Added `DocumentRagFoundry` after `RagFoundryLocal`
  - Added `VisionMemoryAgentSample` and `NpuBenchmarkSample` to image section
- ✅ Added `## HarrierConsoleApp` section (6 progressive examples, int download size guidance)
- ✅ Updated `## RagFoundryLocal` with DocumentRagFoundry section

**Impact:** samples/README.md now accurately reflects all 16 samples on disk, with HarrierConsoleApp fully documented alongside ConsoleApp.

---

### 5. docs/dependency-injection.md — DI Conflict Documentation

**File:** `docs/dependency-injection.md`

**Changes:**
- ✅ Added new section `## Multi-Model Scenarios: DI Registration Conflicts` with:
  - Explanation of TryAddSingleton behavior and first-registration-wins pattern
  - Warning code example showing silent registration skip
  - **Option 1:** Keyed services (recommended for .NET 8+)
  - **Option 2:** Register one via DI, create other explicitly
  - **Option 3:** Wrapper service holding both generators
- ✅ Added new section `## Harrier Integration` with:
  - All 4 overloads of `AddHarrierEmbeddings()` (basic, delegate, options, IConfiguration)
  - Warning about vector store re-indexing
  - Link to full Harrier guide

**Impact:** Developers using both base and Harrier embeddings now have clear guidance on the DI conflict and three working solutions.

---

### 6. src/ElBruno.LocalEmbeddings.Harrier/README.md — Package-Specific README

**File:** `src/ElBruno.LocalEmbeddings.Harrier/README.md` (new)

**Changes:**
- ✅ Created focused package README with:
  - Installation command
  - Quick Start code example (await async, generate, print dimensions)
  - Model Details table (7 properties)
  - Features list (7 bullets with emoji)
  - Configuration section (options, variants table)
  - DI registration example
  - "Learn More" section with links to full guide and sample
  - MIT license reference

**Files also updated:**
- ✅ `ElBruno.LocalEmbeddings.Harrier.csproj` — Changed `<PackageReadmeFile>` from `..\..\README.md` to local `README.md`

**Impact:** When the Harrier package is published to NuGet, users will see Harrier-specific documentation instead of the generic root README. Clear, focused, and links back to the full guide.

---

## Verification

✅ **Build Status:** `dotnet build` — Success  
✅ **Solution State:** All 5 source projects + 5 test projects + 16 samples compile without errors  
✅ **Markdown Validation:** All files use consistent formatting, proper tables, code blocks, and links  
✅ **Branding:** All package and folder references follow `ElBruno.` prefix convention  
✅ **Consistency:** All new documentation mirrors established style (XML docs, DI patterns, Options pattern)

---

## Items NOT Addressed (Out of Scope)

From the original architecture review, these items were identified as Priority 2 or 3 and are outside this docs-only update:

1. **Add `AddHarrierEmbeddings(string modelName)` overload** — Requires API changes (Priority 2, code)
2. **Extract `IHarrierModelDownloader` interface** — Requires refactoring (Priority 3, code)
3. **Fix static HttpClient SEC-002 gap** — Requires security changes (Priority 3, code)
4. **Add IHttpClientFactory integration for Harrier DI** — Requires DI refactor (Priority 3, code)
5. **Add DocumentRagFoundry to slnx** — Requires solution file update (Priority 1, structural)
6. **Add explicit OnnxRuntime/Tokenizers refs in Harrier csproj** — Requires dependency updates (Priority 1, deps)
7. **Remove/document NPU stub directories** — Requires cleanup (Priority 2, structural)

---

## Bottom Line

✅ **All 6 documentation items complete.** Harrier is now fully integrated into project documentation with clear migration paths, DI guidance, and package-specific README. The solution builds successfully and all changes follow established conventions.
