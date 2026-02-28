### 2026-02-28: Pre-merge benchmark baseline captured

**By:** Parker  
**What:** Baseline benchmark results saved to `docs/benchmarks/baseline-pre-merge-improvePerformanceAndSecurity.md` before merging PR #37. Compute-only benchmarks ran successfully on .NET 8.0.24 / X64 RyuJIT AVX2; model-dependent benchmarks documented as requiring local model cache.  
**Why:** Provides a before/after comparison point to validate performance gains from the security+performance remediation branch. Actual numbers: MeanPooling 6.3 μs (128T) / 27.8 μs (512T); FindClosest O(n log k) confirmed at 9–1516 μs across CorpusSize 100–10000; L2Norm 109–259 ns with zero allocations.
