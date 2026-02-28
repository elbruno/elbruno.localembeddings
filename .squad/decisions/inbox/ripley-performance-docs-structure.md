# Ripley Recommendation: Performance Docs Reorganization

**To:** Parker, Bruno  
**From:** Ripley (Lead)  
**Date:** 2026-02-28  
**Re:** Organizing performance documentation under `docs/performance/`

---

## Recommendation

**✅ Adopt a dedicated `docs/performance/` folder structure.**

### Why

1. **Clear entry point** — Users land on `docs/performance/README.md` to see current performance status, benchmark commands, and how to interpret results.
2. **Better discoverability** — Performance docs grouped together; `/benchmarks/` (which feels like build artifacts) no longer houses performance reference docs.
3. **Scalable pattern** — Establish naming conventions for future comparisons (`comparison-YYYYMMDD-pr-NUMBER.md`) and baselines (`baseline-YYYYMMDD-LABEL.md`).
4. **Low risk** — Existing baseline moved as-is; no data loss.

### What to Do

1. Folder `docs/performance/` now exists; see `docs/performance/ORGANIZATION.md` for full structural analysis.

2. **Parker:** 
   - Move/copy `docs/benchmarks/baseline-pre-merge-improvePerformanceAndSecurity.md` → `docs/performance/baseline-pre-merge-improvePerformanceAndSecurity.md`
   - Create `docs/performance/README.md` as the entry point (outline provided in ORGANIZATION.md)
   - Create post-merge comparison doc: `docs/performance/comparison-20260228-pr-37.md` (optional, but recommended)

3. **Optional:** Add a placeholder `docs/benchmarks/README.md` redirecting to `/docs/performance/` (avoids breaking existing bookmarks/links).

### Decision

This formalizes a team pattern for performance documentation going forward. If Parker agrees, we can add a short entry to `.squad/decisions.md` to codify the folder structure and naming conventions.

---

## Next Steps

- Parker executes the file moves and creates `README.md` entry point
- Bruno reviews for completeness
- Optional: Merge `docs/performance/ORGANIZATION.md` as permanent reference for future maintainers
