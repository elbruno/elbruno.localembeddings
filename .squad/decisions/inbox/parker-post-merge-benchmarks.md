# Parker — Post-Merge Benchmark Results Summary

**Date:** 2026-02-28  
**Branch:** main (after merging `improvePerformanceAndSecurity` PR #37)  
**Comparison doc:** `docs/performance/post-merge-comparison.md`

## Overall Verdict

✅ **No regressions.** All statistically meaningful results are neutral or improved. The improvePerformanceAndSecurity merge delivered measurable performance gains on `main`.

## Results by Suite

### L2Normalization — Clear improvement
| Benchmark | Pre | Post | Delta |
|-----------|----:|-----:|-------|
| NormalizeEmbedding_768d | 258.7 ns | 194.0 ns | **−25.0%** |
| NormalizeEmbedding_384d | 109.0 ns | 94.8 ns | **−13.1%** |
- Zero allocations maintained in both cases.

### FindClosest — Consistent improvement, especially at scale
| CorpusSize / TopK | Pre | Post | Delta |
|-------------------|----:|-----:|-------|
| 100 / 5 | 9.533 μs | 8.752 μs | −8.2% |
| 100 / 10 | 8.204 μs | 7.931 μs | −3.3% |
| 100 / 50 | 9.664 μs | 8.865 μs | −8.3% |
| 1000 / 5 | 78.594 μs | 75.535 μs | −3.9% |
| 1000 / 10 | 86.126 μs | 75.904 μs | −11.9% |
| 1000 / 50 | 127.903 μs | 80.487 μs | −37.1% |
| 10000 / 5 | 1,515.868 μs | 998.833 μs | **−34.1%** |
| 10000 / 10 | 1,206.896 μs | 1,196.378 μs | ~0% |
| 10000 / 50 | 1,245.760 μs | 1,143.139 μs | −8.2% |
- O(topK) allocation profile unchanged.

### MeanPooling — Neutral
| Benchmark | Pre | Post | Delta |
|-----------|----:|-----:|-------|
| 128T/768H | 6.281 μs | 6.044 μs | −3.8% |
| 512T/768H | 27.802 μs | 31.922 μs | +14.8% ⚠️ noise |
- The 512T +14.8% is within measurement noise (50.7% CI, ShortRun). Code unchanged.
- 3.02 KB allocation identical (ArrayPool eliminates input GC).

## Recommended Follow-up

- Run `--job default` on MeanPooling 512T to confirm no regression with tighter CI.
- Run model-dependent benchmarks (`EmbeddingGenerationBenchmarks`, `SingleVsBatchBenchmarks`) once models are cached locally to get end-to-end throughput comparison.
