---
name: "ci-linux-test-failures"
description: "Lessons learned from Linux CI test failures in ElBruno.LocalEmbeddings — platform-specific behaviors that break on GitHub Actions Ubuntu runners"
domain: "ci-testing"
confidence: "high"
source: "incident-analysis"
---

## Context

These lessons were learned while diagnosing and fixing a series of Linux CI failures on the `Publish to NuGet` workflow (`publish.yml`) after the v1.1.0 security/performance release. The CI runs on `ubuntu-24.04`. Local dev is Windows. Two distinct categories of failure emerged: test failures and version string failures.

---

## Patterns

### 1. `[Fact]` + `Skip.IfNot(IsWindows())` = Linux FAIL

**Root cause:** `Skip.IfNot(condition, reason)` from the `xunit.extensions.testing` package throws `SkipException` when the condition is false. With `[Fact]`, xUnit treats this as a test **failure**. With `[SkippableFact]`, xUnit intercepts the exception and marks it as a **skip**.

**Rule:** Any test that uses `Skip.If*` or `Skip.IfNot*` MUST be decorated with `[SkippableFact]` or `[SkippableTheory]`, never `[Fact]`/`[Theory]`.

**File fixed:** `tests/ElBruno.LocalEmbeddings.Tests/ModelDownloaderSecurityTests.cs`  
**Test fixed:** `EnsureModelAsync_WindowsAbsolutePath_ThrowsArgumentException`  
**Symptom on CI:** 101 passed, 1 failed, 36 skipped (instead of 101 passed, 37 skipped)

### 2. `Path.GetInvalidFileNameChars()` is OS-specific

**Root cause:** On Linux, `Path.GetInvalidFileNameChars()` returns only `{ '\0', '/' }`. On Windows, it includes `<`, `>`, `:`, `"`, `|`, `?`, `*`, and control characters. Code that validates file name safety using this API will silently accept dangerous characters on Linux.

**Rule:** Never rely on `Path.GetInvalidFileNameChars()` for security validation. Use a hardcoded cross-platform superset:
```csharp
private static readonly char[] _invalidFileNameChars =
    ['<', '>', ':', '"', '|', '?', '*', '\\', '/', '\0'];
```

**File fixed:** `src/ElBruno.LocalEmbeddings.ImageEmbeddings/Options/ImageEmbeddingsOptions.cs`  
**Test that caught it:** `ImageEmbeddingsOptionsValidationTests.FileName_InvalidChars_ThrowsArgumentException`  
**Symptom on CI:** 4 failing tests in `ElBruno.LocalEmbeddings.ImageEmbeddings.Tests`

### 3. Bad git tag format breaks NuGet version extraction

**Root cause:** A git release tag of `v.1.1.2` (note: dot after `v`) is stripped of its leading `v` to produce `.1.1.2`, which is invalid as a NuGet semantic version string. The publish workflow failed at the Build step with: `Invalid NuGet version string: '.1.1.2'`.

**Rule:** The `Determine version` step in `publish.yml` must strip BOTH the leading `v` AND any subsequent leading dot:
```bash
VERSION="${VERSION#v}"   # strip leading v  (v1.2.3 → 1.2.3)
VERSION="${VERSION#.}"   # strip stray dot  (.1.2.3 → 1.2.3)
```
Also add validation so the workflow fails fast with a helpful message if the version is still invalid after stripping.

**File fixed:** `.github/workflows/publish.yml` — `Determine version` step  
**Correct tag format:** `v1.2.3` (no dot between `v` and the version)

---

## Summary Table

| Issue | Platform | Root Cause | Fix |
|-------|----------|------------|-----|
| `[Fact]` + `Skip.IfNot()` → FAIL | Linux only | xUnit needs `[SkippableFact]` to catch `SkipException` | Change `[Fact]` → `[SkippableFact]` |
| `Path.GetInvalidFileNameChars()` misses `<>|?*` | Linux only | OS-specific API returns fewer chars on Linux | Use hardcoded cross-platform char array |
| `Invalid NuGet version string: '.1.1.2'` | CI (any OS) | Tag was `v.1.1.2` instead of `v1.1.2` | Strip both `v` and `.` prefix in workflow |

---

## Anti-Patterns

- **`[Fact]` with `Skip.IfNot()`** — Always pair `Skip.*` with `[SkippableFact]`/`[SkippableTheory]`
- **`Path.GetInvalidFileNameChars()` for security** — Returns different results on Linux vs Windows. Use a hardcoded set for security validation.
- **Trusting `v` prefix alone in tag names** — Users can accidentally create `v.X.Y.Z` tags. Strip both `v` and any leading `.` before using as a NuGet version.
- **Tests that only pass locally on Windows** — Always run `dotnet test` on a non-Windows machine (or CI) before merging security/validation tests that rely on OS-specific APIs.

## Verification Checklist (before merging security tests)

- [ ] All tests using `Skip.If*` or `Skip.IfNot*` are decorated with `[SkippableFact]`/`[SkippableTheory]`
- [ ] No security validation uses `Path.GetInvalidFileNameChars()` — use hardcoded set instead
- [ ] CI passes on Linux (check GitHub Actions) — not just locally on Windows
