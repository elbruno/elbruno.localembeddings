# Lambert Phase 4 Test Report

**Date:** 2026-02-12  
**Author:** Lambert (Tester)  
**Requested by:** Bruno Capuano

## Summary

Wrote Phase 4 unit tests for SEC-002, SEC-009, SEC-007, and PERF-04. All files compile cleanly (`dotnet build` → 0 errors, 0 warnings).

---

## Tests Written

### 1. `ModelDownloaderSecurityTests.cs` (appended)

**Test:** `ModelDownloader_DefaultConstructor_UsesSocketsHttpHandler`  
**Covers:** SEC-002 — parameterless ctor uses `SocketsHttpHandler` with `PooledConnectionLifetime`  
**Approach:** Verifies construction succeeds and `GetCacheDirectory()` returns a non-empty string. Private handler fields are not introspectable, so behavioral verification is used.  
**Status:** ✅ Will pass immediately (construction works today)

---

### 2. `ClipTokenizerFileSizeTests.cs` (new file — ImageEmbeddings.Tests)

**Test 1:** `ClipTokenizer_OversizedVocabFile_ThrowsInvalidOperationException`  
**Covers:** SEC-009 — vocab file >50MB must throw `InvalidOperationException`  
**Approach:** Creates a 51MB sparse file via `FileStream.SetLength`, passes it as vocab path, asserts `InvalidOperationException`.  
**Status:** ⚠️ **TDD test — will FAIL until Ash adds the size guard to `ClipTokenizer.cs`**. The guard is not present in the production code at the time of writing.

**Test 2:** `ClipTokenizer_ValidSizeVocabFile_DoesNotThrowOnSizeCheck`  
**Covers:** SEC-009 — valid-size vocab file must not trigger the guard  
**Approach:** Creates a 22-byte valid JSON vocab file, asserts that any exception thrown does NOT carry the size-guard message.  
**Status:** ✅ Will pass immediately (no size guard fires for a tiny file)

---

### 3. `AsyncPatternTests.cs` (new file — LocalEmbeddings.Tests)

**Test 1:** `LocalEmbeddingGenerator_CreateAsync_MethodExists`  
**Covers:** SEC-007 — async factory documented/available; verifies via reflection  
**Approach:** Uses `GetMethods(Public | Static)` filtered by name `"CreateAsync"` (avoids `AmbiguousMatchException` from three overloads). Asserts all overloads return `Task<LocalEmbeddingGenerator>`.  
**Status:** ✅ Passes (three `CreateAsync` overloads already exist)

**Test 2:** `ServiceCollectionExtensions_AddLocalEmbeddings_RegistersService`  
**Covers:** PERF-04 — DI registration compiles and registers `IEmbeddingGenerator<string, Embedding<float>>`  
**Approach:** Registers with `AddLocalEmbeddings`, checks `IServiceCollection` directly without calling `BuildServiceProvider` (no model files needed).  
**Status:** ✅ Passes

---

## Action Required

**Ash must implement SEC-009** in `ClipTokenizer.cs`:

```csharp
// In ClipTokenizer constructor, before File.ReadAllText:
const long MaxVocabFileSizeBytes = 50L * 1024 * 1024; // 50MB
var fileInfo = new FileInfo(vocabJsonPath);
if (fileInfo.Exists && fileInfo.Length > MaxVocabFileSizeBytes)
{
    throw new InvalidOperationException(
        $"Vocabulary file '{vocabJsonPath}' exceeds the 50MB size limit ({fileInfo.Length:N0} bytes). " +
        "Oversized vocabulary files may indicate a configuration error.");
}
```

Once added, `ClipTokenizer_OversizedVocabFile_ThrowsInvalidOperationException` will pass.
