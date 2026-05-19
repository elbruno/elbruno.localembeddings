# Phase 2 Implementation Roadmap

**Prepared by:** Ripley (Lead Architect)  
**Date:** 2026-05-19  
**Scope:** Native AOT + Quantization API  
**Duration:** ~3 weeks (18 workdays)  
**Team Size:** 4-5 engineers

---

## Phase 2 Overview

**Goal:** Enable serverless deployment via Native AOT + expose quantization controls to users.

**Key Results:**
- ✅ Native AOT builds successfully (PublishAot=true)
- ✅ <5% performance overhead vs non-AOT
- ✅ Quantization API (QuantizationFormat enum) working end-to-end
- ✅ Azure Functions / Lambda templates included
- ✅ Zero breaking changes (backward compatible)
- ✅ Security audit Phase 2 passed

---

## Implementation Timeline

### Week 1: AOT Foundation & Configuration

#### Sprint 1.1 (Days 1-2): AOT Baseline & Dependency Analysis

**Lead:** Architect (Ripley)  
**Tasks:**
- [ ] Run reflection analysis on codebase
  - Search for Type.GetType(), Reflection.Invoke(), etc.
  - Document findings
  - Status: Expected — only in optional config binding
- [ ] Audit all NuGet dependencies for AOT status
  - Check each package against AOT compatibility list
  - Contact ElBruno.HuggingFace.Downloader maintainer if needed
  - Document blocking dependencies (if any)
- [ ] Capture trimmer baseline report
  - Run: `dotnet publish -p:PublishAot=true`
  - Collect warning count and categories
  - Establish baseline for Phase 2 work

**Deliverable:** Analysis report (2-3 pages)

---

#### Sprint 1.2 (Days 3-4): Trimming Metadata Configuration

**Lead:** Implementation Team Lead  
**Tasks:**
- [ ] Update `ElBruno.LocalEmbeddings.csproj`
  ```xml
  <PropertyGroup>
      <PublishTrimmed>true</PublishTrimmed>
      <TrimMode>partial</TrimMode>
      <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
  </PropertyGroup>
  ```
- [ ] Create `TrimmerRoot.cs` (new file)
  - Preserve metadata for LocalEmbeddingGenerator
  - Preserve metadata for ServiceCollectionExtensions
  - Minimal suppressions, maximum AOT safety
- [ ] Run trimmer on net8.0 & net10.0
  - Verify build succeeds with <5 warnings
  - Document any unexpected warnings

**Deliverable:** Updated csproj + TrimmerRoot.cs + trimmer report

---

#### Sprint 1.3 (Days 5): AOT Test Harness

**Lead:** QA / Test Lead  
**Tasks:**
- [ ] Create `AotCompatibilityTests.cs` (new test file)
  - DirectInstantiation_Works test
  - DependencyInjectionWithDelegate_Works test
  - DependencyInjectionWithPrebuiltOptions_Works test
  - ConfigurationBinding_ThrowsWarning test
- [ ] Verify all 4 tests pass
- [ ] Document test coverage for AOT

**Deliverable:** AotCompatibilityTests.cs + passing test runs

---

### Week 2: Quantization API & Validation

#### Sprint 2.1 (Days 6-7): Quantization Options & Enum

**Lead:** Implementation Team Lead  
**Tasks:**
- [ ] Create `QuantizationFormat` enum
  ```csharp
  public enum QuantizationFormat
  {
      Float32,
      Int8,
      Float16
  }
  ```
- [ ] Extend `LocalEmbeddingsOptions`
  - Add `PreferQuantization` property (new)
  - Mark `PreferQuantized` as [Obsolete] but still functional
  - Update documentation
- [ ] Update model resolver logic
  - Modify `ResolveModelPath()` to search for quantized variants
  - Implement fallback chain: Int8 → Float16 → Float32
- [ ] Verify backward compatibility
  - Existing code using `PreferQuantized = true` still works
  - Maps to `PreferQuantization = QuantizationFormat.Int8`

**Deliverable:** Enum + extended options + updated resolver + passing tests

---

#### Sprint 2.2 (Days 8-9): AOT Build Validation

**Lead:** DevOps / Build Engineer  
**Tasks:**
- [ ] Build for .NET 8 with PublishAot=true
  - Command: `dotnet publish -f net8.0 -p:PublishAot=true`
  - Verify executable generated (<100 MB target)
  - Capture metrics
- [ ] Build for .NET 10 with PublishAot=true
  - Verify similar size & performance
- [ ] Performance benchmark (AOT vs JIT)
  - Run embedding generation benchmarks
  - Measure overhead: target <5%
  - Document results
- [ ] Update build documentation
  - Add AOT build instructions to README

**Deliverable:** AOT builds for both frameworks + performance report

---

#### Sprint 2.3 (Day 10): Quantization Model Resolution

**Lead:** Implementation Engineer  
**Tasks:**
- [ ] Test quantization model search logic
  - Create test models: model.onnx, model_quantized.onnx, model_fp16.onnx
  - Verify correct file is selected based on PreferQuantization setting
  - Verify fallback works when quantized variant missing
- [ ] Add quantization tests
  ```csharp
  [Fact]
  public void LoadsInt8Model_WhenAvailable() { ... }
  
  [Fact]
  public void FallsBackToFloat32_WhenInt8Missing() { ... }
  ```
- [ ] Verify no breaking changes
  - All existing tests still pass (314+)
  - New tests added (5-10 quantization-specific)

**Deliverable:** Quantization tests + passing test suite

---

### Week 3: Integration & Release Preparation

#### Sprint 3.1 (Days 11-12): Azure Functions Sample & Testing

**Lead:** Integration / DevOps Engineer  
**Tasks:**
- [ ] Create `samples/AzureFunctionsSample/` (new project)
  - Program.cs with AOT-compatible DI setup
  - GenerateEmbedding HTTP trigger function
  - Quantization enabled (INT8 recommended)
- [ ] Test locally with Azure Functions Core Tools
  - `func start`
  - Invoke function via HTTP
  - Verify response
- [ ] Test Docker deployment
  - Create Dockerfile for AOT app
  - Build and run container
  - Verify cold-start time <2s
- [ ] Document deployment steps
  - Create samples/AzureFunctionsSample/README.md
  - Include deployment guide for Azure Portal

**Deliverable:** AzureFunctionsSample + Docker validation + README

---

#### Sprint 3.2 (Days 13-14): Quantization Accuracy Validation

**Lead:** ML / Data Engineer  
**Tasks:**
- [ ] Download INT8 quantized models
  - test variants for all-MiniLM, all-mpnet, etc.
- [ ] Run accuracy benchmarks
  - STS benchmark (Semantic Textual Similarity)
  - MTEB benchmark (if available)
  - Document accuracy drop for each model
- [ ] Benchmark performance
  - Latency comparison (Float32 vs Int8 vs Float16)
  - Throughput comparison (embeddings/sec)
  - Document speedup factors
- [ ] Create quantization-model-registry.json
  - Update with real benchmark results
  - Include all models & variants

**Deliverable:** Benchmark report + updated registry + accuracy validation

---

#### Sprint 3.3 (Days 15-16): Documentation & Examples

**Lead:** Technical Writer / Architect  
**Tasks:**
- [ ] Create `docs/aot-deployment-guide.md`
  - AOT fundamentals
  - Deployment checklist
  - Troubleshooting guide
- [ ] Create `docs/quantization-guide.md`
  - When to use quantization
  - Accuracy/speed tradeoffs
  - Usage examples
  - Model registry reference
- [ ] Update `README.md`
  - Add AOT deployment section
  - Add quantization examples
  - Add serverless deployment highlights
- [ ] Create migration guide
  - PreferQuantized → PreferQuantization
  - Examples showing both APIs work

**Deliverable:** Comprehensive documentation + examples

---

#### Sprint 3.4 (Days 17): Security Audit Phase 2

**Lead:** Security Engineer (Ash)  
**Tasks:**
- [ ] Review AOT-specific security concerns
  - Reflection lockdown (no new reflection vulnerabilities)
  - Model loading path (no new path traversal)
  - ONNX Runtime native library validation
- [ ] Verify quantization doesn't weaken security
  - Model verification still applies
  - Hash checks still work
  - No new credential exposure
- [ ] Approve AOT + Quantization for release
  - Green light for Phase 2 release
  - Document any recommendations

**Deliverable:** Security audit report + approval

---

#### Sprint 3.5 (Day 18): Release Preparation & Testing

**Lead:** Release Manager  
**Tasks:**
- [ ] Run full test suite
  - `dotnet test` — verify all 314+ tests pass
  - Run new AOT & quantization tests
  - Run integration tests
- [ ] Build final NuGet package
  - Bump version to 1.5.0 (minor)
  - Update package metadata
  - Include README in package
- [ ] Create GitHub release
  - Draft release notes summarizing Phase 2
  - Link to architecture docs
  - Note breaking changes (none!)
- [ ] Final sign-off checklist
  - All code reviewed
  - All tests passing
  - Performance validated
  - Security approved
  - Documentation complete

**Deliverable:** Final NuGet package + release notes + GitHub release

---

## Team Assignments

| Role | Workdays | Tasks |
|------|----------|-------|
| **Architect (Ripley)** | 4 | AOT analysis, design, documentation |
| **Implementation Lead** | 6 | AOT config, quantization options, model resolver |
| **QA/Test Lead** | 4 | Test harness, accuracy validation, test suite |
| **DevOps Engineer** | 3 | AOT builds, Docker, Azure Functions |
| **ML/Data Engineer** | 3 | Benchmarks, accuracy validation, model registry |
| **Technical Writer** | 2 | Documentation, migration guide |
| **Security Engineer (Ash)** | 1 | Security audit Phase 2 |
| **Release Manager** | 1 | Final testing, package, release |
| **Total** | **24** | — |

---

## Risk Mitigation

| Risk | Severity | Mitigation |
|------|----------|-----------|
| ONNX Runtime AOT incompatibility | HIGH | Contact vendor early; plan workaround |
| Quantization accuracy drops >2% | MEDIUM | Benchmark early, choose conservative models |
| Cold-start latency >2s | MEDIUM | Validate with Azure Functions locally first |
| Dependency AOT incompatibility | MEDIUM | Identify blocking deps in week 1 |
| Performance regression | LOW | Benchmark continuously, <5% target |

---

## Success Criteria (Go/No-Go)

### Must-Have (Go)
- ✅ AOT builds successfully on .NET 8 & 10
- ✅ All 314+ existing tests pass
- ✅ Quantization API works end-to-end
- ✅ Zero breaking changes
- ✅ Security audit Phase 2 passed

### Nice-to-Have (Nice)
- ✅ <5% performance overhead
- ✅ AWS Lambda sample
- ✅ Google Cloud sample
- ✅ Quantization accuracy benchmarks for all models

---

## Deliverables Summary

### Code Changes
1. `ElBruno.LocalEmbeddings.csproj` (AOT configuration)
2. `TrimmerRoot.cs` (new trimming metadata)
3. `QuantizationFormat.cs` (new enum)
4. `LocalEmbeddingsOptions.cs` (extended with PreferQuantization)
5. `LocalEmbeddingGenerator.cs` (updated model resolver)
6. `AotCompatibilityTests.cs` (new test class)
7. `QuantizationTests.cs` (new test class)
8. `samples/AzureFunctionsSample/` (new sample project)

### Documentation
1. `docs/phase2-native-aot-quantization-architecture.md`
2. `docs/aot-validation-checklist.md`
3. `docs/aot-deployment-guide.md` (new)
4. `docs/quantization-guide.md` (new)
5. `docs/quantization-model-registry.md`
6. Updated `README.md`
7. Migration guide (new)
8. Release notes

### Samples & References
1. Azure Functions sample with AOT
2. Dockerfile for AOT deployment
3. Quantization model registry (JSON)
4. Performance benchmarks report
5. Security audit report

---

## Go-Live Readiness

**Before shipping Phase 2:**
- [ ] All sprints complete
- [ ] All tests passing
- [ ] Security approved
- [ ] Documentation reviewed
- [ ] Performance validated
- [ ] Team sign-off

**Release:** ~3 weeks from approval

---

**Ripley — Lead Architect**  
*Driving Phase 2 to completion.*

---

**End of Implementation Roadmap**
