# Phase 2 Test Strategy — Documentation Index

**Created:** 2026-05-19 | **By:** Lambert (Tester) | **Status:** ✅ Complete & Ready

This index guides you through the comprehensive Phase 2 test strategy for ElBruno.LocalEmbeddings.

---

## 📚 Four Core Documents

### 1. **PHASE2_TEST_STRATEGY.md** — The Master Plan (26 KB)
**Read this first if:** You're leading Phase 2 testing or need the complete picture.

**Contains:**
- Full test matrix: 55+ tests across Native AOT, Quantization, OpenTelemetry, Streaming
- Detailed test scenarios (test IDs, acceptance criteria, edge cases)
- Performance baselines and regression detection thresholds
- Coverage targets (88% line coverage, <2% flake rate)
- Effort estimation (77 story points over 6 weeks)
- CI/CD workflow recommendations

**Key Sections:**
- Section 2: Features & Test Scope (break down per feature)
- Section 3: Test Matrix (one-page reference)
- Section 7: Coverage Targets & Success Criteria
- Section 11: CI/CD Integration

---

### 2. **PHASE2_TEST_QUICKREF.md** — The Checklist (9.3 KB)
**Read this if:** You're implementing Phase 2 tests or need quick reference.

**Contains:**
- Test checklist by feature (7-17 tests per feature)
- Test execution commands (local & CI)
- Common issues & troubleshooting (6 scenarios)
- Go/No-Go release criteria
- Performance baseline locks

**Use for:**
- Daily standup status updates
- Tracking which tests are complete
- Running tests locally: `dotnet test tests/ElBruno.LocalEmbeddings.Tests/Quantization/`
- Debugging flaky tests

---

### 3. **PHASE2_TEST_SCAFFOLDING.md** — Implementation Templates (19 KB)
**Read this if:** You're writing Phase 2 test code.

**Contains:**
- Complete directory structure (where to create test files)
- Copy-paste-ready C# templates:
  - Feature test classes (QuantizationAccuracyTests.cs)
  - Shared fixtures (ModelFixture.cs)
  - Helper utilities (CosineSimilarityCalculator.cs)
  - Test data fixtures (TestDataFixture.cs)
- Test data file formats (CSV, JSONL, JSON)
- GitHub Actions workflow template
- MSBuild integration snippets

**Use for:**
- Creating new test files (copy template, fill in scenarios)
- Understanding test data layout
- Setting up CI workflows

---

### 4. **PHASE2_TEST_SUMMARY.md** — Executive Overview (7.2 KB)
**Read this if:** You need a 5-minute overview or to brief stakeholders.

**Contains:**
- Test coverage breakdown (by feature)
- Timeline & effort estimates
- Critical tests (must-pass for release)
- Success criteria (Go/No-Go gates)
- Next steps for implementation team

---

## 🎯 Quick Navigation

### By Role

**🏗️ Architect (Ash):**
1. PHASE2_TEST_STRATEGY.md (Section 2.1: AOT architecture constraints)
2. PHASE2_TEST_STRATEGY.md (Section 4: Coverage targets)

**👨‍💻 Developer (Implementation):**
1. PHASE2_TEST_QUICKREF.md (Test checklist)
2. PHASE2_TEST_SCAFFOLDING.md (Copy templates)
3. PHASE2_TEST_STRATEGY.md (Feature details as needed)

**🧪 QA / Integration (Kane):**
1. PHASE2_TEST_QUICKREF.md (Test checklist)
2. PHASE2_TEST_STRATEGY.md (Section 3: Test matrix)
3. PHASE2_TEST_STRATEGY.md (Section 11: CI/CD)

**📊 Project Lead (Bruno):**
1. PHASE2_TEST_SUMMARY.md (30-second overview)
2. PHASE2_TEST_STRATEGY.md (Sections 6-7: Effort & success criteria)

---

### By Phase

**📋 Week 1-2: Planning & Prep**
- Read: PHASE2_TEST_STRATEGY.md (Sections 1-3)
- Read: PHASE2_TEST_SUMMARY.md (Timeline)
- Action: Create test directories from PHASE2_TEST_SCAFFOLDING.md

**💻 Week 2-3: AOT & Quantization Tests**
- Reference: PHASE2_TEST_QUICKREF.md (AOT + Quantization checklists)
- Copy: PHASE2_TEST_SCAFFOLDING.md (Test templates)
- Implement: 13 AOT + 13 Quantization tests

**📡 Week 3-4: OpenTelemetry Tests**
- Reference: PHASE2_TEST_QUICKREF.md (OpenTelemetry checklist)
- Implement: 14 OpenTelemetry tests (spans + metrics)

**🌊 Week 4-5: Streaming Expansion**
- Reference: PHASE2_TEST_QUICKREF.md (Streaming checklist)
- Implement: 14 Streaming tests (buffer + E2E)

**🏆 Week 5-6: Baselines & UAT**
- Lock: Performance baselines (Section 11 in PHASE2_TEST_STRATEGY.md)
- Validate: Manual smoke tests (Windows + Linux)
- Go/No-Go: Release criteria (PHASE2_TEST_SUMMARY.md)

---

## 📊 Test Inventory

### By Feature (55+ Tests Total)

#### Native AOT (13 tests)
**Unit (5):** Reflection, serialization, type registration  
**Integration (4):** Model loading, async cleanup, batch ops  
**E2E (4):** Publish, cold start, memory, warnings  
**Location:** `tests/ElBruno.LocalEmbeddings.Tests/AOT/`

#### Quantization (13 tests)
**Unit (5):** Enum parsing, model selection, validation  
**Integration (8):** Accuracy >0.99, latency <80%, fallback  
**Location:** `tests/ElBruno.LocalEmbeddings.Tests/Quantization/`

#### OpenTelemetry (14 tests)
**Unit (7):** Span emission, metric generation, logging  
**Integration (7):** Export validation, performance overhead, structure  
**Location:** `tests/ElBruno.LocalEmbeddings.Tests/OpenTelemetry/`

#### Streaming (14 tests)
**Unit (4):** Buffer management, option parsing, validation  
**Integration (10):** 100K vectors, memory O(buffer), cancellation  
**Location:** `tests/ElBruno.LocalEmbeddings.Tests/Streaming/`

---

## 🗂️ Test Data

### Pre-Cached Models (CI)
- `all-minilm-l6-v2` (full): 34 MB
- `all-minilm-l6-v2` (INT8): 10 MB
- `e5-small` (full): 30 MB
- `e5-small` (INT8): 9 MB

### Test Data Files (Location: `tests/test-data/`)
- **semantic-pairs.csv** — 100 text pairs with similarity scores
- **batch-texts-1k.jsonl** — 1K line-delimited JSON
- **edge-cases.json** — 50 edge case samples

*See PHASE2_TEST_SCAFFOLDING.md for file formats.*

---

## 🚀 Getting Started

### Minimum Viable Setup (Day 1)
1. Create directories: `tests/ElBruno.LocalEmbeddings.Tests/{AOT,Quantization,OpenTelemetry,Streaming}/`
2. Copy files from PHASE2_TEST_SCAFFOLDING.md:
   - `Fixtures/` (ModelFixture.cs, TestDataFixture.cs)
   - `Helpers/` (CosineSimilarityCalculator.cs, MemoryProfiler.cs)
3. Create `test-data/` with sample semantic-pairs.csv
4. Add xUnit + Moq NuGet references to test .csproj

### First Test Implementation (Day 1-2)
1. Pick simplest test: QNT-U-001 (enum parsing)
2. Copy template from PHASE2_TEST_SCAFFOLDING.md (QuantizationOptionsTests.cs)
3. Fill in arrange/act/assert
4. Run: `dotnet test tests/ElBruno.LocalEmbeddings.Tests/Quantization/QuantizationOptionsTests.cs`
5. Iterate on remaining unit tests for Quantization

### First Integration Test (Day 3)
1. Pick: QNT-I-001 (load quantized model)
2. Use ModelFixture.cs for model caching
3. Run test locally with pre-downloaded model
4. Add to CI workflow

---

## 📈 Success Metrics

✅ **Coverage:** 88%+ line coverage on Phase 2 new code  
✅ **Reliability:** <2% flake rate (max 1 flaky test per 50)  
✅ **Performance:** Baselines locked by Week 6  
✅ **Regression:** Zero performance regressions >10%  
✅ **Documentation:** Every test has clear purpose + expected behavior  

---

## ⚠️ Critical Tests (Must-Pass for Release)

These 6 tests are gate-keepers for Phase 2 release:

1. **AOT-E2E-001** — Publish as self-contained Native AOT binary
2. **AOT-E2E-004** — Zero trimming warnings during publish
3. **QNT-I-003** — Cosine similarity >0.99 (quantized vs. full)
4. **QNT-I-004** — Quantized latency <80% of full-precision
5. **STR-M-001** — Streaming 100K vectors <150MB memory
6. **OTEL-P-002** — Telemetry overhead <5%

All other tests are important but these 6 determine release readiness.

---

## 📞 Questions?

| Question | Answer Location |
|----------|-----------------|
| "How do I run tests locally?" | PHASE2_TEST_QUICKREF.md (Test Run Examples) |
| "What's the full test matrix?" | PHASE2_TEST_STRATEGY.md (Section 3) |
| "How do I implement a new test?" | PHASE2_TEST_SCAFFOLDING.md (Feature Test Template) |
| "What are the acceptance criteria?" | PHASE2_TEST_STRATEGY.md (Sections 2.1-2.4) |
| "When should baselines be locked?" | PHASE2_TEST_QUICKREF.md (Performance Baseline Locks) |
| "What's the Go/No-Go criteria?" | PHASE2_TEST_SUMMARY.md (Success Criteria) |

---

## 📋 Version History

| Date | Version | Status | Author |
|------|---------|--------|--------|
| 2026-05-19 | 1.0 | ✅ Complete | Lambert |
| 2026-05-19 | 1.0 | ✅ Ready for Week 1 | Lambert |

---

## 🎬 Next Action

→ **Start Week 1 with:** PHASE2_TEST_QUICKREF.md (take checklist to standup)  
→ **For implementation:** PHASE2_TEST_SCAFFOLDING.md (copy templates)  
→ **For architecture review:** PHASE2_TEST_STRATEGY.md (Sections 2.1-2.4)

---

**Maintained by:** Lambert (Tester)  
**Last Updated:** 2026-05-19  
**Next Review:** Post-Week-1 Implementation (2026-05-26)
