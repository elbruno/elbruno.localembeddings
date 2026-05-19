# PHASE 2: OPENTELEMETRY TRACE ARCHITECTURE & SPAN DESIGN

**Prepared by:** Kane (Integration Specialist)  
**Date:** 2026-05-26  
**Status:** ✅ Design Complete

---

## Executive Summary

This document specifies the **complete trace architecture** for OpenTelemetry instrumentation of ElBruno.LocalEmbeddings. It defines span hierarchies, attribute schemas, error handling strategies, and root cause analysis patterns for enterprise observability.

---

## 1. Span Hierarchy & Activity Names

### 1.1 Activity Naming Convention

```
Namespace: ElBruno.LocalEmbeddings
Format: ElBruno.LocalEmbeddings.<OperationName>

Examples:
- ElBruno.LocalEmbeddings.GenerateEmbeddings    (root or child)
- ElBruno.LocalEmbeddings.LoadModel              (root, may block GenerateEmbeddings)
- ElBruno.LocalEmbeddings.BatchGenerate          (child of GenerateEmbeddings)
- ElBruno.LocalEmbeddings.StreamingGenerate      (root for streams)
- ElBruno.LocalEmbeddings.VectorSearch           (child of app operation)
```

### 1.2 Span Hierarchy Tree

```
HTTP Request (ASP.NET Core middleware)
│
├─ ElBruno.LocalEmbeddings.GenerateEmbeddings [ROOT]
│  ├─ ElBruno.LocalEmbeddings.LoadModel [CHILD]
│  │  ├─ ElBruno.LocalEmbeddings.ValidateCache [GRANDCHILD]
│  │  ├─ ElBruno.LocalEmbeddings.DownloadModel [GRANDCHILD]
│  │  └─ ElBruno.LocalEmbeddings.ApplyQuantization [GRANDCHILD]
│  │
│  ├─ ElBruno.LocalEmbeddings.BatchGenerate [CHILD] (repeated for each batch)
│  │  └─ [Tokenization, ONNX Inference, Normalization - implicit in ONNX runtime]
│  │
│  └─ ElBruno.LocalEmbeddings.PostProcessing [CHILD] (optional, if normalization enabled)
│
├─ ElBruno.LocalEmbeddings.StreamingGenerate [ROOT]
│  ├─ ElBruno.LocalEmbeddings.StreamBuffer [CHILD] (one per batch)
│  ├─ ElBruno.LocalEmbeddings.BatchGenerate [CHILD] (one per batch)
│  └─ ElBruno.LocalEmbeddings.StreamYield [CHILD] (optional, for yield timing)
│
└─ ElBruno.LocalEmbeddings.VectorSearch [ROOT or CHILD]
   ├─ ElBruno.LocalEmbeddings.CorpusLoad [CHILD]
   ├─ ElBruno.LocalEmbeddings.SimilarityCompute [CHILD] (SIMD operations)
   └─ ElBruno.LocalEmbeddings.RankResults [CHILD]
```

**Root vs. Child determination:**
- **Root span:** Called from user code (e.g., `GenerateAsync()`, `FindClosestAsync()`)
- **Child span:** Called from instrumented code (e.g., `BatchGenerate` called by `GenerateEmbeddings`)
- Framework (ASP.NET) sets `Activity.Current` → we link as child if span exists

---

## 2. Comprehensive Span Attribute Schema

### 2.1 GenerateEmbeddings Span

**Activity Name:** `ElBruno.LocalEmbeddings.GenerateEmbeddings`  
**Typical Duration:** 5ms - 500ms  
**Status:** OK (no error), ERROR (exception)

**Span Attributes:**

```csharp
// Semantic attributes (OpenTelemetry standards)
activity.SetTag("llm.system", "local-embeddings");
activity.SetTag("llm.request.model", options.ModelName);              // e.g., "sentence-transformers/all-MiniLM-L6-v2"
activity.SetTag("llm.request.type", "text");                         // "text" or "image"
activity.SetTag("llm.usage.input_tokens", totalTokenCount);          // e.g., 500
activity.SetTag("llm.usage.output_dimension", embeddingDimension);   // e.g., 384
activity.SetTag("llm.quantization_format", quantFormat);             // "int8", "float32", "none"

// Custom attributes (ElBruno-specific)
activity.SetTag("custom.input_count", texts.Count());
activity.SetTag("custom.batch_count", batchCount);
activity.SetTag("custom.batch_size_target", options.BatchSize);
activity.SetTag("custom.batch_size_actual", actualBatchSize);
activity.SetTag("custom.cache_status", "hit" | "miss");
activity.SetTag("custom.model_load_time_ms", modelLoadMs);
activity.SetTag("custom.inference_time_ms", inferenceMs);
activity.SetTag("custom.normalize_embeddings", options.NormalizeEmbeddings);

// Status codes
activity.SetStatus(ActivityStatusCode.Ok | ActivityStatusCode.Error);
if (exception != null)
    activity.SetStatus(ActivityStatusCode.Error, "Exception during generation");

// Error details (if exception)
activity.RecordException(exception);
activity.SetTag("error.type", exception.GetType().Name);
activity.SetTag("error.message", exception.Message);
```

**Example trace in Jaeger:**

```
Span: GenerateEmbeddings (elapsed: 87ms)
├─ start_time: 2026-05-26T10:15:30.123Z
├─ tags:
│  ├─ llm.system: "local-embeddings"
│  ├─ llm.request.model: "sentence-transformers/all-MiniLM-L6-v2"
│  ├─ llm.usage.input_tokens: 500
│  ├─ llm.usage.output_dimension: 384
│  ├─ llm.quantization_format: "int8"
│  ├─ custom.input_count: 16
│  ├─ custom.batch_count: 1
│  ├─ custom.batch_size_actual: 16
│  ├─ custom.cache_status: "hit"
│  ├─ custom.inference_time_ms: 8.2
│  └─ otel.status_code: "OK"
├─ end_time: 2026-05-26T10:15:30.230Z
└─ duration: 87ms
```

### 2.2 LoadModel Span

**Activity Name:** `ElBruno.LocalEmbeddings.LoadModel`  
**Typical Duration:** 50ms (cache hit) - 3s (cold load)  
**Status:** OK, ERROR, TIMEOUT

**Span Attributes:**

```csharp
activity.SetTag("llm.system", "local-embeddings");
activity.SetTag("llm.request.model", modelName);
activity.SetTag("custom.cache_status", "hit" | "miss" | "invalid");
activity.SetTag("custom.model_file_size_mb", fileSizeMb);
activity.SetTag("custom.model_dimension", dimension);
activity.SetTag("custom.quantization_format", format);  // "int8", "float32"
activity.SetTag("custom.quantized_file_exists", hasBothVariants);

// Child operations
activity.AddEvent("cache_validation_started");
activity.AddEvent("model_download_started");  // if cache miss
activity.AddEvent("onnx_session_created");
activity.AddEvent("model_load_completed");

// Timings (optional if EnableDetailedEvents=true)
activity.SetTag("custom.cache_check_ms", cacheCheckMs);
activity.SetTag("custom.download_time_ms", downloadMs);  // 0 if cache hit
activity.SetTag("custom.onnx_load_time_ms", onnxLoadMs);
```

**Error handling:**

```csharp
try
{
    // Load model
}
catch (HttpRequestException ex)
{
    activity.SetStatus(ActivityStatusCode.Error, "Download failed");
    activity.RecordException(ex);
    activity.SetTag("error.type", "HttpRequestException");
    activity.SetTag("error.http_status", statusCode);
    throw;
}
catch (InvalidOperationException ex) when (ex.Message.Contains("hash mismatch"))
{
    activity.SetStatus(ActivityStatusCode.Error, "Integrity check failed");
    activity.RecordException(ex);
    activity.SetTag("error.type", "IntegrityCheckException");
    activity.SetTag("custom.expected_hash", expectedHash);
    activity.SetTag("custom.actual_hash", actualHash);
    throw;
}
```

### 2.3 BatchGenerate Span

**Activity Name:** `ElBruno.LocalEmbeddings.BatchGenerate`  
**Typical Duration:** 0.5ms - 50ms (depends on batch size)  
**Parent:** GenerateEmbeddings or StreamingGenerate

**Span Attributes:**

```csharp
activity.SetTag("llm.system", "local-embeddings");
activity.SetTag("llm.request.model", modelName);
activity.SetTag("custom.batch_number", batchIndex);              // 1, 2, 3, ...
activity.SetTag("custom.batch_size", texts.Count);              // actual size
activity.SetTag("custom.total_tokens_in_batch", tokenCount);
activity.SetTag("custom.quantization_format", format);

// Timing breakdown (optional)
activity.SetTag("custom.tokenization_ms", tokenMs);
activity.SetTag("custom.onnx_inference_ms", inferenceMs);
activity.SetTag("custom.normalization_ms", normalizeMs);        // if enabled
activity.SetTag("custom.total_batch_ms", totalMs);
```

### 2.4 StreamingGenerate Span

**Activity Name:** `ElBruno.LocalEmbeddings.StreamingGenerate`  
**Typical Duration:** 100ms - 30s (entire stream)  
**Children:** LoadModel, StreamBuffer, BatchGenerate (multiple)

**Span Attributes:**

```csharp
activity.SetTag("llm.system", "local-embeddings");
activity.SetTag("llm.request.model", modelName);
activity.SetTag("custom.buffer_size", bufferSize);
activity.SetTag("custom.stream_item_count", totalItems);  // set at end
activity.SetTag("custom.batch_count_target", expectedBatches);
activity.SetTag("custom.cancellation_token_set", cancellationToken != default);

// For stream completion
activity.AddEvent("streaming_completed", new ActivityTagsCollection(new[]
{
    new KeyValuePair<string, object?>("total_items_yielded", totalYielded),
    new KeyValuePair<string, object?>("batches_processed", batchCount),
}));
```

### 2.5 VectorSearch Span

**Activity Name:** `ElBruno.LocalEmbeddings.VectorSearch`  
**Typical Duration:** 0.1ms - 50ms (depends on corpus size)  
**Parent:** User application operation

**Span Attributes:**

```csharp
activity.SetTag("llm.system", "local-embeddings");
activity.SetTag("custom.corpus_size", corpusSize);
activity.SetTag("custom.top_k", k);
activity.SetTag("custom.embedding_dimension", dimension);
activity.SetTag("custom.similarity_threshold", threshold);  // if used

// Results
activity.SetTag("custom.results_returned", resultCount);
activity.SetTag("custom.similarity_metric", "cosine");

// Timing breakdown
activity.SetTag("custom.corpus_load_ms", loadMs);
activity.SetTag("custom.similarity_computation_ms", computeMs);  // SIMD
activity.SetTag("custom.ranking_ms", rankMs);
```

---

## 3. Event Taxonomy

**Events are recorded within spans to mark significant points in execution:**

### 3.1 Standard Events

```csharp
// In GenerateEmbeddings
activity.AddEvent("model_loaded");
activity.AddEvent("embedding_generation_started");
activity.AddEvent("embedding_batch_completed", new ActivityTagsCollection(new[]
{
    new KeyValuePair<string, object?>("batch_index", 1),
    new KeyValuePair<string, object?>("duration_ms", 8.2),
}));

// In LoadModel
activity.AddEvent("cache_validation_started");
activity.AddEvent("cache_validated", new ActivityTagsCollection(new[]
{
    new KeyValuePair<string, object?>("cache_status", "hit"),
}));
activity.AddEvent("onnx_session_created");

// In StreamingGenerate
activity.AddEvent("buffer_full", new ActivityTagsCollection(new[]
{
    new KeyValuePair<string, object?>("items_buffered", 32),
}));
activity.AddEvent("batch_submitted");
activity.AddEvent("batch_results_yielded");

// In VectorSearch
activity.AddEvent("corpus_loaded");
activity.AddEvent("similarity_computation_started");
activity.AddEvent("results_ranked");
```

### 3.2 Error Events

```csharp
// Cancel requested mid-stream
activity.AddEvent("cancellation_requested", new ActivityTagsCollection(new[]
{
    new KeyValuePair<string, object?>("items_processed", processedCount),
}));

// Recoverable error
activity.AddEvent("batch_retry", new ActivityTagsCollection(new[]
{
    new KeyValuePair<string, object?>("retry_number", 1),
    new KeyValuePair<string, object?>("error_type", "OutOfMemory"),
}));
```

---

## 4. Error Handling in Spans

### 4.1 Exception Recording

```csharp
public async Task<IEnumerable<Embedding<float>>> GenerateAsync(
    IEnumerable<string> values,
    EmbeddingGenerationOptions? options = null,
    CancellationToken cancellationToken = default)
{
    using var activity = ActivitySource.StartActivity("ElBruno.LocalEmbeddings.GenerateEmbeddings");
    if (activity == null) return await _generator.GenerateAsync(values, options, cancellationToken);

    try
    {
        // Processing...
    }
    catch (OperationCanceledException ex)
    {
        activity.SetStatus(ActivityStatusCode.Error, "Operation cancelled");
        activity.RecordException(ex);
        activity.SetTag("error.type", "OperationCanceledException");
        throw;
    }
    catch (InvalidOperationException ex)
    {
        activity.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity.RecordException(ex);
        activity.SetTag("error.type", "InvalidOperationException");
        throw;
    }
    catch (Exception ex)
    {
        activity.SetStatus(ActivityStatusCode.Error, "Unexpected error");
        activity.RecordException(ex);
        activity.SetTag("error.type", ex.GetType().Name);
        throw;
    }
}
```

### 4.2 Status Codes

| Status | Meaning | When Set |
|--------|---------|----------|
| Unset | Execution ongoing | Initial state |
| OK | Success | Operation completed successfully |
| Error | Failure | Exception occurred or explicit error recorded |

```csharp
// Success
activity.SetStatus(ActivityStatusCode.Ok);

// Error with description
activity.SetStatus(ActivityStatusCode.Error, "Failed to download model");
```

---

## 5. Baggage Propagation

### 5.1 W3C Baggage Header Format

```
baggage: trace.user_id=user-12345,trace.request_id=req-xyz,
         trace.correlation_context=tenant_id%3Dacme
```

### 5.2 Baggage Attachment to Spans

```csharp
public static void AttachBaggageToActivity(Activity activity)
{
    var baggageItems = Baggage.GetBaggage();
    foreach (var item in baggageItems)
    {
        activity.SetTag($"baggage.{item.Key}", item.Value);
    }
}

// Usage in instrumentation
using var activity = ActivitySource.StartActivity("ElBruno.LocalEmbeddings.GenerateEmbeddings");
if (activity != null)
{
    AttachBaggageToActivity(activity);
    // ... rest of operation
}
```

### 5.3 Cross-Service Correlation

```
Service A (API Gateway)
  ├─ Create baggage: trace.request_id=req-xyz, trace.user_id=user-123
  └─ Call ElBruno.LocalEmbeddings (propagates baggage via W3C header)
  
Service B (ElBruno.LocalEmbeddings)
  ├─ Receive baggage via Activity.Current
  ├─ Create GenerateEmbeddings span
  ├─ Span attributes include: baggage.trace.request_id, baggage.trace.user_id
  └─ All child spans inherit baggage
  
Service C (Vector Store)
  ├─ Receive baggage from ElBruno
  ├─ Use baggage for correlation
  └─ Entire trace visible in Jaeger/Datadog
```

---

## 6. Root Cause Analysis Examples

### 6.1 Scenario: "Model Load Timeouts"

```
Symptom: Embedding generation takes >5s intermittently

Trace Investigation:
1. GenerateEmbeddings span = 5200ms (expected: <100ms)
2. Child span: LoadModel = 5150ms (most of the time)
3. Child of LoadModel: DownloadModel = 5100ms
   ├─ Tags: cache_status = "miss"
   ├─ custom.model_file_size_mb = 356
   └─ error.http_status = 429 (rate-limited)
4. Event: "cache_validation_started" at 5.2s
5. Event: "model_download_started" at 5.3s

Root Cause: HuggingFace CDN rate limiting on first load
Solution: Enable model caching or use local model path
```

### 6.2 Scenario: "Quantized Model Crashes"

```
Symptom: IntegrityCheckException when using PreferQuantized=true

Trace Investigation:
1. LoadModel span status = ERROR
2. Child span: ApplyQuantization failed
   ├─ error.type = "IntegrityCheckException"
   ├─ custom.expected_hash = "abc123..."
   ├─ custom.actual_hash = "def456..."
   └─ Event: "cache_validation_started" failed
3. Baggage: model_version = v1.9 (deprecated)

Root Cause: Corrupted int8 model in cache from v1.9
Solution: Clear cache directory and re-download
```

### 6.3 Scenario: "Vector Search Returns No Results"

```
Symptom: FindClosestAsync() returns 0 results on valid corpus

Trace Investigation:
1. VectorSearch span status = OK (but results_returned = 0)
   ├─ custom.corpus_size = 1000
   ├─ custom.top_k = 5
   ├─ custom.embedding_dimension = 768
   └─ custom.results_returned = 0
2. Baggage: dataset_id = old_dataset_v1
3. Spanning events show: corpus_loaded, similarity_computation_started, results_ranked

Root Cause: Corpus embeddings are 384-dim, query is 768-dim
Solution: Re-embed corpus with current model or use matching model
```

### 6.4 Scenario: "Memory Leak in Streaming"

```
Symptom: Memory grows continuously during streaming generation

Trace Investigation:
1. StreamingGenerate span duration = 45s (expected: 10s for same data)
2. Multiple StreamBuffer child spans:
   ├─ Span 1: 100ms
   ├─ Span 2: 110ms
   ├─ Span 3: 125ms
   ├─ ...
   ├─ Span N: 5000ms (increases over time)
3. Activity memory not being disposed
4. Baggage: enable_detailed_events = true (extra overhead)

Root Cause: Activity not disposed in finally block
Solution: Use `using (var activity = ...)` or try-finally
```

---

## 7. Debug Attributes (Development Only)

**When EnableDetailedEvents=true and IncludeEmbeddingVectorData=true:**

```csharp
// WARNING: Sensitive data! Development only!
activity.SetTag("debug.input_text_sample", texts.FirstOrDefault()?[..50]);  // First 50 chars
activity.SetTag("debug.output_vector_sample", embeddings.First().Span[..5].ToString());
activity.SetTag("debug.model_load_stacktrace", stackTrace);
activity.SetTag("debug.onnx_runtime_version", OnnxRuntimeVersion);
```

**Never include in production traces!**

---

## 8. Span Timing Verification

**Expected timings (on modern hardware with cache hits):**

| Operation | P50 | P95 | P99 |
|-----------|-----|-----|-----|
| GenerateEmbeddings (10 texts, batch=32) | 3ms | 8ms | 12ms |
| GenerateEmbeddings (100 texts, batch=32) | 25ms | 85ms | 120ms |
| LoadModel (warm cache, int8) | 30ms | 45ms | 60ms |
| VectorSearch (1000 corpus, top-5) | 0.8ms | 1.2ms | 1.5ms |
| StreamingGenerate (10K items) | 800ms | 1200ms | 1500ms |

**If timings exceed these, investigation spans should explain why** (e.g., "cache miss", "network latency", "low memory").

---

## Next Document

See **phase2-otel-metrics-design.md** for metrics schemas and Prometheus/Grafana integration.
