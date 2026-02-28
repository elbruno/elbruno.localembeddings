# Performance Docs Organization — Analysis & Recommendation

**Author:** Ripley (Lead)  
**Date:** 2026-02-28

---

## Current State

### What Exists Today

- **`docs/benchmarks/baseline-pre-merge-improvePerformanceAndSecurity.md`**
  - Pre-merge baseline from PR #37 (security+performance audit branch)
  - Contains compute benchmarks (MeanPooling, FindClosest, L2Norm) with explicit instructions for running post-merge comparison
  - Provides BenchmarkDotNet output format and hardware context

- **`docs/alternative-models.md`**
  - Configuration and usage guidance for alternative embedding models
  - Not specifically performance-focused, but relevant to end-user experience

- **No dedicated entry point for performance overview or status**

### Current Limitations

1. **No clear entry point** — Users unsure where to find current vs. historical performance info
2. **Benchmark results live in `/benchmarks/` which is confusing** — suggests build artifacts rather than reference docs
3. **No pattern established for storing post-merge comparisons** or future benchmark results
4. **Discoverability issue** — Performance docs scattered or buried; not easy to surface latest status

---

## Proposed Structure

```
docs/performance/
├── README.md                                      ← Entry point: current status
├── baseline-pre-merge-improvePerformanceAndSecurity.md  ← Historical baseline
├── (future) comparison-20260228-pr-37.md         ← Post-merge comparison pattern
├── (future) baseline-20260301-YYYYMMDD.md       ← Historical baselines pattern
└── (future) quarterly-summary-2026-Q1.md        ← Optional: trend analysis
```

### Entry Point Document (`docs/performance/README.md`)

**Proposed outline:**

```markdown
# Performance Guide

## Current Performance Status

- Link to latest benchmark comparison (e.g., post-PR #37 results)
- Headline metrics (latency, throughput, allocations) for common scenarios
- Quick link to baseline for comparison

## Running Benchmarks Locally

### Compute-Only Benchmarks (No Model Required)

```bash
dotnet run --project benchmarks/ElBruno.LocalEmbeddings.Benchmarks \
  -c Release --framework net8.0 -- --filter "*MeanPoolingBenchmarks*" --job short
```

### Model-Dependent Benchmarks (Requires Cached Models)

```bash
dotnet run --project benchmarks/ElBruno.LocalEmbeddings.Benchmarks \
  -c Release --framework net8.0 -- --filter "*EndToEndBenchmarks*" --job short
```

## Interpreting Results

- **Mean (μs):** Average latency in microseconds
- **Gen0 / Allocated:** Garbage collection pressure and heap allocations
- **Error / StdDev:** Variation across runs
- See BenchmarkDotNet docs for detailed interpretation

## Historical Baselines & Comparisons

- [Baseline (Pre-PR #37)](./baseline-pre-merge-improvePerformanceAndSecurity.md)
- [Post-PR #37 Comparison](./comparison-20260228-pr-37.md)

## Performance Notes

- ONNX session is reused (singleton, thread-safe)
- TensorPrimitives leverage SIMD for cosine similarity, L2 norm, mean pooling
- Top-K search uses PriorityQueue min-heap for O(n log k) complexity
- Model download caching; set `LocalEmbeddingsOptions.ExpectedHash` for integrity verification
```

---

## Pros

| Aspect | Benefit |
|--------|---------|
| **Clear Entry Point** | Users see `docs/performance/README.md` and immediately understand current status and how to benchmark |
| **Grouped Documentation** | All performance-related docs in one place; easy to discover and maintain |
| **Discoverability** | "Performance" as a folder name is self-explanatory; better than `/benchmarks/` (which feels like build artifacts) |
| **Scalable Pattern** | As team adds post-merge comparisons and quarterly trends, folder structure is ready to scale |
| **Backward Compatibility** | Historical baseline preserved exactly as-is; no data loss |
| **User Experience** | README entry point acts as a landing page that guides readers to historical data, replication steps, and interpretation guidance |
| **Team Clarity** | Establishment of naming convention (`comparison-YYYYMMDD-pr-NUMBER.md`, `baseline-YYYYMMDD-label.md`) prevents drift and makes handoffs predictable |

---

## Cons / Risks

| Risk | Mitigation |
|------|-----------|
| **Folder creation overhead** | Minimal; moving one file is a single operation |
| **Existing links** | Only the internal link in baseline doc may break; easily fixable with a note or redirect in benchmarks/ README |
| **Namespace collision** | Could confuse `/benchmarks/` (build output) with `/docs/performance/` (reference docs). **Mitigation:** Move baseline now; deprecate `/benchmarks/` for performance docs (keep only if build artifacts need a home) |
| **Maintenance burden** | If README falls out of date, stale instructions hurt users. **Mitigation:** Include in PR review checklist: update README.md when adding new benchmarks |

---

## Recommendation

**✅ ADOPT THIS STRUCTURE.**

### Action Plan

1. **Create `docs/performance/` folder** (if not already present)

2. **Move or link the baseline:**
   - Copy `docs/benchmarks/baseline-pre-merge-improvePerformanceAndSecurity.md` → `docs/performance/baseline-pre-merge-improvePerformanceAndSecurity.md`
   - Update internal links in that file if needed (e.g., relative path adjustments)
   - **Deprecate** `/docs/benchmarks/` for performance docs; keep only if build artifacts need a home

3. **Create `docs/performance/README.md`** with the outline above

4. **Establish a pattern in the team decision log:**
   - New benchmark comparisons follow: `comparison-YYYYMMDD-pr-NUMBER.md`
   - New baselines follow: `baseline-YYYYMMDD-LABEL.md`
   - Update `README.md` with links whenever new files are added

5. **Update `.squad/decisions.md`** if Parker or the team wants to codify the performance documentation strategy

---

## What to Do with `/docs/benchmarks/`

- **Option A (Recommended):** Keep the folder but change its purpose:
  - Create a **`docs/benchmarks/README.md`** stating: "Performance documentation has moved to `/docs/performance/`. See that folder for current benchmarks, baselines, and historical comparisons."
  - Remove the markdown files; they now live in `/docs/performance/`

- **Option B:** Delete entirely if `/docs/benchmarks/` is no longer used

**We recommend Option A** (a placeholder README) to avoid 404s if anyone has bookmarked or linked to `/docs/benchmarks/` externally.

---

## Summary

The proposed `docs/performance/` structure improves **discoverability**, provides a **clear entry point**, and establishes a **scalable pattern** for future benchmark results. Moving the baseline and creating a README is a low-risk change that pays dividends as the team adds more performance documentation over time.

**Status:** Ready for Parker to execute (actual file creation and population).
