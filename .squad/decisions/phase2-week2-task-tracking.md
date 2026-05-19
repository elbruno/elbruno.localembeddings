# Phase 2 Week 2 - Task Tracking

**Week Goal:** Cold-start measurement + Azure Functions integration + Quantization testing + Baseline lock  
**Status:** Day 1/5 Complete (Cold-Start Measurement Harness)  
**Overall Progress:** 1 of 4 objectives complete (25%)

---

## WEEK 2 OBJECTIVES CHECKLIST

### ✅ OBJECTIVE 1: Cold-Start Measurement Harness (1.5 days) — COMPLETE
- [x] Create console app: `src/Samples/AotColdStartMeasurement/`
- [x] Measure wall-clock startup time from process launch
- [x] Generate 10 embeddings with latency tracking for:
  - [x] Process startup (cold)
  - [x] Model load (first call)
  - [x] First embedding generation
  - [x] Subsequent embedding latency (steady-state)
- [x] Target achieved: Total cold-start <2 seconds (1823ms achieved ✅)
- [x] Export results to CSV for trending
- **Completion:** 2026-05-19, 15:40 UTC
- **Deliverable:** `tests/performance-baseline.json` + CSV export

---

### 🔲 OBJECTIVE 2: Azure Functions Emulator Testing (2 days) — PENDING
**Start Date:** 2026-05-20 (expected)

#### Tasks
- [ ] **Task 2.1:** Install Azure Functions Core Tools
  - [ ] Check if Docker is available (preferred: official Azure Functions image)
  - [ ] Alternative: Direct install on Windows
  - [ ] Verify: `func --version` reports 4.x+
  - **Effort:** 30 minutes
  
- [ ] **Task 2.2:** Create local Azure Functions project
  - [ ] Directory: `src/Samples/AotAzureFunctionsLocal/`
  - [ ] Create HttpTrigger function: `GenerateEmbedding`
  - [ ] Endpoint: `POST /api/GenerateEmbedding`
  - [ ] Input: JSON body with text to embed
  - [ ] Output: JSON with embedding vector + metadata
  - **Effort:** 1 hour
  
- [ ] **Task 2.3:** Integrate ElBruno.LocalEmbeddings
  - [ ] Add project reference to main library
  - [ ] Register `IEmbeddingGenerator<string, Embedding<float>>` in DI
  - [ ] Implement function logic: call generator, return result
  - **Effort:** 30 minutes

- [ ] **Task 2.4:** Run in Azure Functions emulator
  - [ ] Start local emulator: `func host start`
  - [ ] Run cold-start test: measure time from launch to first request response
  - [ ] Target: ~2s cold-start (same as standalone harness)
  - [ ] Record timing data
  - **Effort:** 30 minutes

- [ ] **Task 2.5:** Test graceful failure modes
  - [ ] Missing model file → should return 500 with clear error
  - [ ] Bad config (invalid model name) → should return 400 with validation error
  - [ ] Empty text input → should return 400 (validation)
  - [ ] Concurrent requests (5x parallel) → should not crash
  - **Effort:** 1 hour

- [ ] **Task 2.6:** Document results
  - [ ] Record cold-start time in serverless context
  - [ ] Note any differences vs standalone harness
  - [ ] Document failure mode behavior
  - **Effort:** 30 minutes

**Objective 2 Total Effort:** 4-5 hours (1.5 days @ 3 hrs/day)

---

### 🔲 OBJECTIVE 3: Quantization Integration Test (1 day) — PENDING
**Start Date:** 2026-05-22 (expected)

#### Tasks
- [ ] **Task 3.1:** Identify quantization models
  - [ ] Check if quantized ONNX models available in cache
  - [ ] Models to test: Float32 (baseline), INT8, Float16, INT4 (if available)
  - [ ] Alternative: Use mock data or skip INT4 if unavailable
  - **Effort:** 30 minutes

- [ ] **Task 3.2:** Create quantization benchmark
  - [ ] New console app: `src/Samples/AotQuantizationBenchmark/`
  - [ ] Load each quantization variant
  - [ ] Generate 100 embeddings per variant
  - [ ] Measure latency and accuracy
  - **Effort:** 2 hours

- [ ] **Task 3.3:** Accuracy testing
  - [ ] Generate embeddings for semantic test pairs
  - [ ] Calculate cosine similarity
  - [ ] Compare vs baseline: must preserve >0.99 accuracy
  - [ ] Document any accuracy loss
  - **Effort:** 1 hour

- [ ] **Task 3.4:** Latency comparison
  - [ ] Tabulate: Latency by quantization type
  - [ ] Calculate speedup factors (% faster than baseline)
  - [ ] Expected: INT8 ~40% faster, Float16 ~50% faster
  - **Effort:** 30 minutes

- [ ] **Task 3.5:** Deploy to Azure Functions
  - [ ] Test each quantization variant in Azure Functions emulator
  - [ ] Measure cold-start with quantized models
  - [ ] Verify: cold-start still <2s with INT8/Float16
  - **Effort:** 1 hour

- [ ] **Task 3.6:** Document findings
  - [ ] Create `docs/quantization-benchmarks.md`
  - [ ] Include: latency table, accuracy comparison, speedup factors
  - [ ] Recommendation: which quantization to use for serverless
  - **Effort:** 1 hour

**Objective 3 Total Effort:** 6-7 hours (1 day @ 7 hrs)

---

### 🔲 OBJECTIVE 4: Performance Baseline Lock (0.5 days) — PARTIAL
**Start Date:** 2026-05-23 (expected)

#### Tasks
- [x] **Task 4.1:** Record measurements in JSON
  - [x] Format: `{ aot_cold_start_ms: 1823, ... }`
  - [x] Location: `tests/performance-baseline.json`
  - [x] Completed: 2026-05-19

- [ ] **Task 4.2:** Integrate baseline check into CI/CD
  - [ ] Update `.github/workflows/build.yml`
  - [ ] Add step: Load baseline, run cold-start harness, compare
  - [ ] Fail build if: `aot_cold_start_ms > 2000`
  - [ ] Report: Show difference vs baseline (pass/fail with margin)
  - **Effort:** 1 hour

- [ ] **Task 4.3:** Document baseline update procedure
  - [ ] Create `docs/performance-baseline-update.md`
  - [ ] Explain: When to update baseline (breaking changes, hardware migration)
  - [ ] Process: Manual run, review results, commit new baseline
  - [ ] Approval: Require code review for baseline increases >50ms
  - **Effort:** 30 minutes

- [ ] **Task 4.4:** Verify CI/CD integration
  - [ ] Push test commit, verify GitHub Actions runs baseline check
  - [ ] Confirm: Build passes (baseline within gate)
  - [ ] Simulate failure: Introduce slowness, verify build fails
  - **Effort:** 30 minutes

**Objective 4 Total Effort:** 2-2.5 hours (0.5-1 day)

---

## TIMELINE & DEPENDENCIES

```
Day 1 (Today): ✅ Cold-Start Harness
  └─ Completes: Baseline measurement, CSV export

Day 2-3: Azure Functions Setup
  ├─ Depends: Day 1 (baseline data)
  ├─ Task: Install Functions Core Tools
  ├─ Task: Create sample function app
  └─ Task: Measure cold-start in serverless

Day 4: Quantization Testing
  ├─ Depends: Day 1 (model availability), Day 3 (if testing in Functions)
  ├─ Task: Create benchmark app
  ├─ Task: Test accuracy & latency
  └─ Task: Compare vs baseline

Day 5: Baseline Lock & CI/CD
  ├─ Depends: Day 1, 3, 4 (all measurements complete)
  ├─ Task: Update GitHub Actions workflow
  ├─ Task: Verify build fails if baseline exceeded
  └─ Task: Document process for future updates
```

---

## BLOCKERS TO MONITOR

| Blocker | Probability | Mitigation |
|---------|-------------|-----------|
| Azure Functions Core Tools unavailable | Low | Use Docker image |
| Quantization models missing | Medium | Proceed with Float32 only |
| Cold-start >2s detected | Critical | Escalate immediately; investigate ONNX Runtime config |
| GitHub Actions quota exceeded | Low | Check runner minutes balance |
| Model download times | Medium | Pre-cache models in CI/CD environment |

---

## ROLLBACK PROCEDURES

If any cold-start measurement shows degradation:

1. **If cold-start >2s:**
   - [ ] Revert last commit (git revert)
   - [ ] Investigate root cause:
     - ONNX Runtime version change?
     - Model cache invalidation?
     - System load/resource contention?
   - [ ] Re-run measurement 3x to confirm (account for variance)
   - [ ] If confirmed issue: escalate to Phase Lead (Ripley)

2. **If Azure Functions integration breaks:**
   - [ ] Revert Functions sample
   - [ ] Verify standalone harness still passes
   - [ ] File issue: document failure, attach logs
   - [ ] Wait for investigation before re-attempting

3. **If CI/CD baseline check breaks build:**
   - [ ] Disable check (comment out step)
   - [ ] Merge with override
   - [ ] File issue: investigate False Positive
   - [ ] Re-enable after root cause fixed

---

## SUCCESS CRITERIA FOR WEEK 2

| Criterion | Target | Status |
|-----------|--------|--------|
| Cold-start <2s | 1823ms | ✅ PASS |
| Azure Functions cold-start <2s | ~2000ms | 🔲 PENDING |
| Quantization latency improvement | +40% (INT8) | 🔲 PENDING |
| Quantization accuracy preserved | >0.99 | 🔲 PENDING |
| Baseline locked in CI/CD | Fail if >2000ms | 🔲 PENDING |
| Zero build failures | 0 errors | ✅ PASS (baseline achieved) |
| Documentation complete | 3+ docs | ✅ 1 (need +2 more) |

---

## NOTES FOR TEAM

- **Dallas (Agent):** Implementing all Week 2 objectives sequentially
- **Ripley (Phase Lead):** Monitor for cold-start degradation; escalate if >2.1s detected
- **Parker (Performance):** Available to review quantization benchmarks and micro-optimizations
- **Kane (Architecture):** Standby for Azure Functions integration questions

---

**Updated:** 2026-05-19 15:40 UTC  
**Next Review:** 2026-05-20 (end of Day 2)  
**Target Completion:** 2026-05-23 (end of Day 5)
