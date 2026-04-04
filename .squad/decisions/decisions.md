# Squad Decisions Log

## 2026-02-28: Cross-Project Tracking Issue for Security & Performance Lessons

**By:** Ripley (Lead)  
**Date:** 2026-02-28  
**Status:** Implemented  
**Issue:** #38

Created GitHub issue #38 to track application of v1.1.0 security and performance lessons across 9 related ElBruno projects. The audit identified 26 findings (9 security + 17 performance) plus 3 critical CI/Linux patterns that are systematic issues likely existing in other projects.

**Decision:** Going forward, audit teams will create similar tracking issues for cross-project lessons. Reference this pattern when scaling improvements across the ElBruno portfolio. Pin issue #38 to LocalEmbeddings for visibility.

**Related:** Issue #38, Security Audit Findings, Performance Audit Findings, CI/Linux patterns.

---

## 2026-02-28: Performance Documentation Reorganization

**By:** Ripley (Lead)  
**To:** Parker, Bruno  
**Date:** 2026-02-28  
**Re:** Organizing performance documentation under `docs/performance/`

**Decision:** ✅ Adopt a dedicated `docs/performance/` folder structure.

**Rationale:** Clear entry point for users, better discoverability, scalable pattern for future comparisons (naming: `comparison-YYYYMMDD-pr-NUMBER.md`) and baselines (`baseline-YYYYMMDD-LABEL.md`).

**Actions:**
1. Move `docs/benchmarks/baseline-pre-merge-improvePerformanceAndSecurity.md` → `docs/performance/baseline-pre-merge-improvePerformanceAndSecurity.md`
2. Create `docs/performance/README.md` as entry point
3. Optional: Create post-merge comparison doc `docs/performance/comparison-20260228-pr-37.md`
4. Optional: Add `docs/benchmarks/README.md` placeholder redirecting to `/docs/performance/`

---

## 2026-02-28: Pre-Merge Benchmark Baseline Captured

**By:** Parker (Performance Engineer)  
**Date:** 2026-02-28  
**Status:** Complete

Baseline benchmark results saved to `docs/benchmarks/baseline-pre-merge-improvePerformanceAndSecurity.md` before merging PR #37. Compute-only benchmarks ran successfully on .NET 8.0.24 / X64 RyuJIT AVX2.

**Results:** MeanPooling 6.3 μs (128T) / 27.8 μs (512T); FindClosest O(n log k) at 9–1516 μs (CorpusSize 100–10000); L2Norm 109–259 ns with zero allocations.

---

## 2026-02-28: Phase 5 Benchmark Infrastructure Expansion Implemented

**By:** Parker (Performance Engineer)  
**Date:** 2025-07-16 (implemented in current session)  
**Status:** Implemented

Created benchmark project `benchmarks/ElBruno.LocalEmbeddings.Benchmarks/` with 8 benchmark classes covering gaps identified in Phase 1 audit.

**Coverage:** ModelLoading, MeanPooling, EmbeddingGeneration, Tokenizer, FindClosest, L2Normalization, SingleVsBatch, QuantizedVsFull.

**CI Safety:** All model-dependent benchmarks use nullable fields and early guards. Compile and run cleanly in CI with zero-duration results when model cache unavailable.

---

## 2026-02-28: Post-Merge Benchmark Results Summary

**By:** Parker (Performance Engineer)  
**Date:** 2026-02-28  
**Branch:** main (after merging improvePerformanceAndSecurity PR #37)

✅ **No regressions.** All statistically meaningful results are neutral or improved.

**Key Results:**
- **L2Normalization:** −25.0% (768d), −13.1% (384d), zero allocations maintained
- **FindClosest:** Consistent improvement; −37.1% at CorpusSize 1000/TopK 50; −34.1% at 10000/5
- **MeanPooling:** Neutral (512T +14.8% within measurement noise, ShortRun, 50.7% CI)

**Recommended Follow-up:** Run `--job default` on MeanPooling 512T for tighter CI; benchmark model-dependent suites with cached models.

---

## 2026-02-23: Team Founding Decision

**By:** Dallas  
**Date:** 2026-02-23

Initial squad ceremonies and team identity established.
