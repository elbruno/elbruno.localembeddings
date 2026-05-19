# PHASE 2: OPENTELEMETRY IMPLEMENTATION ROADMAP & TESTING STRATEGY

**Prepared by:** Kane (Integration Specialist)  
**Date:** 2026-05-26  
**Status:** ✅ Implementation Roadmap Ready

---

## Executive Summary

This document provides the **week-by-week implementation roadmap** for Phase 2 OpenTelemetry integration, including dependency management, testing strategies, rollout approach, and production hardening checklist.

**Target Timeline:** 4 weeks (Weeks 1-4 of Phase 2)  
**Team Size:** 2-3 engineers  
**Risk Level:** Low (optional package, zero breaking changes)

---

## 1. Implementation Timeline

### Week 1: Core Infrastructure & GenerateEmbeddings Span

**Objective:** Foundation for tracing and metrics, first span implemented.

**Tasks:**

1. **Project Setup** (2 hours)
   - Create `src/ElBruno.LocalEmbeddings.OpenTelemetry/` directory
   - Create `.csproj` file with `net8.0;net10.0` targets
   - Add NuGet dependencies: `OpenTelemetry.Api`, `OpenTelemetry.Sdk`
   - Add to `ElBruno.LocalEmbeddings.slnx`

   ```xml
   <!-- ElBruno.LocalEmbeddings.OpenTelemetry.csproj -->
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
       <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
       <Nullable>enable</Nullable>
       <RootNamespace>ElBruno.LocalEmbeddings</RootNamespace>
       <AssemblyName>ElBruno.LocalEmbeddings.OpenTelemetry</AssemblyName>
       <PackageId>ElBruno.LocalEmbeddings.OpenTelemetry</PackageId>
       <Version>2.0.0-beta.1</Version>
     </PropertyGroup>
     <ItemGroup>
       <PackageReference Include="OpenTelemetry.Api" Version="1.9.0" />
       <PackageReference Include="OpenTelemetry.Sdk" Version="1.9.0" />
     </ItemGroup>
     <ItemGroup>
       <ProjectReference Include="../ElBruno.LocalEmbeddings/ElBruno.LocalEmbeddings.csproj" />
     </ItemGroup>
   </Project>
   ```

2. **Activity Tags Constants** (1 hour)
   - Create `Internal/ActivityTags.cs` with constant attribute names
   - Follow OpenTelemetry semantic conventions

   ```csharp
   // Internal/ActivityTags.cs
   internal static class ActivityTags
   {
       public const string LlmSystem = "llm.system";
       public const string LlmRequestModel = "llm.request.model";
       public const string LlmRequestType = "llm.request.type";
       public const string LlmUsageInputTokens = "llm.usage.input_tokens";
       public const string LlmUsageOutputDimension = "llm.usage.output_dimension";
       public const string LlmQuantizationFormat = "llm.quantization_format";
       public const string LlmCacheStatus = "llm.cache_status";
       public const string CustomBatchSize = "custom.batch_size_actual";
       public const string ErrorType = "error.type";
   }
   ```

3. **Metrics Infrastructure** (3 hours)
   - Create `Metrics/EmbeddingMetrics.cs` with Meter setup
   - Implement histogram and counter recording methods
   - Create `Metrics/MetricsCollector.cs` for aggregation helpers
   - Write unit tests (10 test cases):
     - Histogram recording with tags
     - Counter increment with attributes
     - Gauge updates
     - Meter disposal

4. **ActivitySource Setup** (2 hours)
   - Create `ActivitySource` in DI container
   - Configure sampling rate
   - Add to telemetry options

5. **Options Class** (1 hour)
   - Create `Options/LocalEmbeddingsOpenTelemetryOptions.cs`
   - Properties: `EnableTracing`, `EnableMetrics`, `EnableBaggage`, `SamplingRate`
   - Default values: all true, sampling=1.0

**Deliverables:**
- ✅ ElBruno.LocalEmbeddings.OpenTelemetry project structure
- ✅ EmbeddingMetrics class with 5 histograms, 5 counters
- ✅ 15 unit tests (>90% coverage)
- ✅ Zero breaking changes in core library

**Build & Test:**
```bash
dotnet build
dotnet test
```

---

### Week 2: Instrumentation Wrapper & Core Spans

**Objective:** Implement decorator pattern, instrument GenerateEmbeddings and LoadModel.

**Tasks:**

1. **InstrumentedEmbeddingGenerator Wrapper** (4 hours)
   - Create `Instrumentation/InstrumentedEmbeddingGenerator.cs`
   - Wrap core library's `IEmbeddingGenerator<string, Embedding<float>>`
   - Delegate to wrapped instance
   - Inject EmbeddingMetrics and ActivitySource
   - Implement GenerateAsync with span creation

   ```csharp
   public class InstrumentedEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
   {
       private readonly IEmbeddingGenerator<string, Embedding<float>> _inner;
       private readonly ActivitySource _activitySource;
       private readonly EmbeddingMetrics _metrics;
       
       public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(...)
       {
           using var activity = _activitySource.StartActivity("ElBruno.LocalEmbeddings.GenerateEmbeddings");
           if (activity == null) return await _inner.GenerateAsync(...);
           
           // Record metrics and attributes
           var startTime = DateTime.UtcNow;
           try
           {
               var result = await _inner.GenerateAsync(...);
               activity.SetStatus(ActivityStatusCode.Ok);
               var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
               _metrics.RecordEmbeddingGenerationDuration(...);
               return result;
           }
           catch (Exception ex)
           {
               activity.SetStatus(ActivityStatusCode.Error);
               activity.RecordException(ex);
               _metrics.IncrementErrors(...);
               throw;
           }
       }
   }
   ```

2. **ModelLoader Instrumentation** (3 hours)
   - Create `Instrumentation/ModelLoaderInstrumenter.cs`
   - Span: `ElBruno.LocalEmbeddings.LoadModel`
   - Track cache hits/misses
   - Record model load duration

3. **Service Registration Extension** (2 hours)
   - Create `Extensions/ServiceCollectionExtensions.cs`
   - Implement `AddLocalEmbeddingsOpenTelemetry()` method
   - Validate core library registered first
   - Register metrics, ActivitySource, decorator

4. **Unit Tests for Instrumentation** (3 hours)
   - Test span creation on GenerateAsync call
   - Verify attributes set correctly
   - Test error handling and exception recording
   - Test decorator delegation
   - Mock tests using Moq (20 test cases)

**Deliverables:**
- ✅ InstrumentedEmbeddingGenerator decorator
- ✅ ModelLoaderInstrumenter with spans
- ✅ DI registration extension method
- ✅ 20+ unit tests

**Build & Test:**
```bash
dotnet build
dotnet test --filter "InstrumentedEmbeddingGenerator or ModelLoader"
```

---

### Week 3: Streaming & Advanced Features, Baggage Propagation

**Objective:** Complete streaming instrumentation, add W3C baggage support, performance tuning.

**Tasks:**

1. **Streaming Instrumentation** (3 hours)
   - Create `Instrumentation/StreamingEmbeddingsInstrumenter.cs`
   - Span: `ElBruno.LocalEmbeddings.StreamingGenerate`
   - Child spans: `StreamBuffer`, `BatchGenerate` (for each batch)
   - Track total items yielded

2. **Vector Search Instrumentation** (2 hours)
   - Create `Instrumentation/VectorSearchInstrumenter.cs`
   - Span: `ElBruno.LocalEmbeddings.VectorSearch`
   - Attributes: corpus_size, top_k, similarity_metric
   - Record search duration

3. **Baggage Propagation** (3 hours)
   - Create `Internal/BaggageExtensions.cs`
   - Implement W3C baggage header parsing
   - Attach baggage items to span attributes
   - Create `ActivityBaggageProvider` for DI

   ```csharp
   internal static class BaggageExtensions
   {
       public static void AttachBaggageToActivity(Activity activity, 
           LocalEmbeddingsOpenTelemetryOptions options)
       {
           if (!options.EnableBaggage) return;
           
           var baggageItems = Baggage.GetBaggage();
           foreach (var item in baggageItems)
               activity.SetTag($"baggage.{item.Key}", item.Value);
           
           foreach (var custom in options.BaggageItems)
               activity.SetTag($"baggage.{custom.Key}", custom.Value);
       }
   }
   ```

4. **Performance Tuning** (2 hours)
   - Verify <2% overhead with benchmarks
   - Test sampling (0.0, 0.1, 0.5, 1.0 rates)
   - Profile Activity creation cost
   - Optimize attribute setting

5. **Unit Tests** (2 hours)
   - Streaming span hierarchy tests
   - Vector search span attributes
   - Baggage propagation tests
   - Sampling validation (15+ test cases)

6. **Build & Integration Check** (1 hour)
   - Verify all projects build
   - Run full test suite
   - Check code coverage (target: >80%)

**Deliverables:**
- ✅ Streaming instrumentation with child spans
- ✅ Vector search instrumentation
- ✅ Baggage propagation with W3C support
- ✅ Performance verified <2% overhead
- ✅ 30+ additional unit tests

**Build & Test:**
```bash
dotnet build
dotnet test --logger "console;verbosity=minimal"
```

---

### Week 4: Exporter Examples, Documentation, Production Hardening

**Objective:** Integration examples, documentation, beta release readiness.

**Tasks:**

1. **Exporter Examples** (3 hours)
   - Create sample code for Jaeger exporter
   - Create sample code for Datadog exporter
   - Create sample code for Azure Monitor exporter
   - Document in `phase2-otel-examples.md` (separate document)

2. **Error Handling & Edge Cases** (2 hours)
   - Test cancellation token handling
   - Test null input handling
   - Test exception propagation
   - Test concurrent operations

3. **Documentation Updates** (2 hours)
   - Update README.md with OpenTelemetry section
   - Add example to docs/
   - Document breaking-change policy (zero breaking changes)
   - Add troubleshooting guide

4. **Performance Validation** (1 hour)
   - Run benchmarks with OTel enabled
   - Compare to baseline (verify <2% overhead)
   - Document results in implementation notes

5. **Code Review & Polish** (1 hour)
   - Code style alignment with project standards
   - XML documentation completeness (100% public APIs)
   - No compiler warnings
   - Nullable reference type validation

6. **Prepare Beta Release** (1 hour)
   - Update version to 2.0.0-beta.1
   - Create NuGet package locally
   - Test package installation
   - Document known limitations

**Deliverables:**
- ✅ Complete examples for Jaeger, Datadog, Azure Monitor
- ✅ Full documentation in `phase2-otel-examples.md`
- ✅ README.md updated with OpenTelemetry section
- ✅ Performance validation report
- ✅ Beta release ready

**Final Build & Test:**
```bash
dotnet clean
dotnet build
dotnet test
dotnet pack
```

---

## 2. Testing Strategy

### 2.1 Unit Test Categories

**Category 1: Span Creation & Attributes** (10 tests)
```csharp
[Theory]
[InlineData("sentence-transformers/all-MiniLM-L6-v2", "int8")]
[InlineData("sentence-transformers/all-MiniLM-L6-v2", "float32")]
public void GenerateAsync_CreatesSpanWithCorrectAttributes(
    string modelName, string quantizationFormat)
{
    // Arrange
    var activity = new List<Activity>();
    using var listener = new ActivityListener
    {
        ShouldListenTo = _ => true,
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => 
            ActivitySamplingResult.AllData
    };
    listener.ActivityStarted += (act) => activity.Add(act);
    ActivitySource.AddActivityListener(listener);
    
    // Act
    var result = await _generator.GenerateAsync(["test text"]);
    
    // Assert
    Assert.Single(activity);
    Assert.Equal("ElBruno.LocalEmbeddings.GenerateEmbeddings", 
        activity[0].OperationName);
    Assert.Equal(modelName, 
        activity[0].GetTagItem("llm.request.model"));
    Assert.Equal(quantizationFormat, 
        activity[0].GetTagItem("llm.quantization_format"));
}
```

**Category 2: Error Handling** (8 tests)
```csharp
[Fact]
public async Task GenerateAsync_OnException_RecordsErrorStatus()
{
    // Arrange
    var mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
    mockGenerator.Setup(g => g.GenerateAsync(It.IsAny<IEnumerable<string>>(), ...))
        .ThrowsAsync(new InvalidOperationException("Test error"));
    
    // Act & Assert
    await Assert.ThrowsAsync<InvalidOperationException>(
        () => _wrapper.GenerateAsync(["test"]));
    
    // Verify span status was set to Error
    Assert.Empty(_capturedActivities);  // Error still recorded in listener
}
```

**Category 3: Metrics Recording** (7 tests)
```csharp
[Fact]
public void RecordEmbeddingGenerationDuration_RecordsHistogramObservation()
{
    // Arrange
    var metricCollected = false;
    using var listener = new MeterListener();
    listener.InstrumentPublished += (instrument, provider) =>
    {
        if (instrument.Name == "elbruno_embedding_generation_duration_ms")
            listener.EnableMeasurementEvents(instrument);
    };
    listener.MeasurementsCompleted += (_, _) => metricCollected = true;
    listener.Start();
    
    // Act
    _metrics.RecordEmbeddingGenerationDuration(8.5, "all-MiniLM-L6-v2", "int8", 32);
    
    // Assert
    Assert.True(metricCollected);
}
```

**Category 4: Baggage Propagation** (5 tests)
```csharp
[Fact]
public void AttachBaggageToActivity_AttachesBaggageItems()
{
    // Arrange
    Baggage.SetBaggage(new[]
    {
        new KeyValuePair<string, string>("trace.user_id", "user-123"),
        new KeyValuePair<string, string>("trace.request_id", "req-xyz")
    });
    var activity = new Activity("test").Start();
    var options = new LocalEmbeddingsOpenTelemetryOptions { EnableBaggage = true };
    
    // Act
    BaggageExtensions.AttachBaggageToActivity(activity, options);
    
    // Assert
    Assert.Equal("user-123", activity.GetTagItem("baggage.trace.user_id"));
    Assert.Equal("req-xyz", activity.GetTagItem("baggage.trace.request_id"));
}
```

**Category 5: DI Registration** (6 tests)
```csharp
[Fact]
public void AddLocalEmbeddingsOpenTelemetry_RegistersServices()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddLocalEmbeddings();
    
    // Act
    services.AddLocalEmbeddingsOpenTelemetry(opts => opts.EnableTracing = true);
    
    // Assert
    var provider = services.BuildServiceProvider();
    var metrics = provider.GetRequiredService<EmbeddingMetrics>();
    var activitySource = provider.GetRequiredService<ActivitySource>();
    Assert.NotNull(metrics);
    Assert.NotNull(activitySource);
}
```

**Total: 40+ unit tests, target >85% code coverage**

### 2.2 Integration Tests

```csharp
[Fact]
public async Task FullPipeline_WithOpenTelemetry_GeneratesTracesAndMetrics()
{
    // Arrange
    var activities = new List<Activity>();
    using var listener = SetupActivityListener(activities);
    
    var services = new ServiceCollection();
    services.AddLocalEmbeddings()
        .AddLocalEmbeddingsOpenTelemetry();
    var provider = services.BuildServiceProvider();
    
    var generator = provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
    var metrics = provider.GetRequiredService<EmbeddingMetrics>();
    
    // Act
    var result = await generator.GenerateAsync(
        new[] { "hello world", "test" },
        cancellationToken: default
    );
    
    // Assert
    Assert.NotNull(result);
    Assert.NotEmpty(activities);
    Assert.Equal("ElBruno.LocalEmbeddings.GenerateEmbeddings", 
        activities[0].OperationName);
    Assert.Equal(ActivityStatusCode.Ok, activities[0].Status);
}
```

### 2.3 Performance Tests

```csharp
[Benchmark(Baseline = true)]
public async Task GenerateEmbeddings_Baseline()
{
    var generator = GetBaselineGenerator();
    await generator.GenerateAsync(TestTexts, default);
}

[Benchmark]
public async Task GenerateEmbeddings_WithOpenTelemetry()
{
    var generator = GetInstrumentedGenerator();
    await generator.GenerateAsync(TestTexts, default);
}

// Expected result: WithOpenTelemetry / Baseline <= 1.02 (2% overhead)
```

---

## 3. Rollout Strategy

### 3.1 Beta Release (Week 4)

**Version:** `2.0.0-beta.1`  
**Availability:** NuGet prerelease feed  
**Duration:** 2 weeks  

**Beta Features:**
- Core tracing (GenerateEmbeddings, LoadModel)
- Basic metrics (histograms, counters)
- W3C baggage propagation
- DI registration extension

**Known Limitations:**
- Streaming span hierarchy (defer to beta.2)
- Vector search instrumentation (defer to beta.2)
- Azure Monitor exporter samples (defer to 2.0.0)

**Feedback Collection:**
- GitHub Discussions for feedback
- Telemetry export error reporting
- Performance metrics collection

### 3.2 Release Candidate (Week 6)

**Version:** `2.0.0-rc.1`  
**Availability:** NuGet prerelease feed

**RC Changes:**
- Add streaming instrumentation
- Add vector search instrumentation
- Performance tuning complete
- Exporter examples finalized

### 3.3 General Availability (Week 8)

**Version:** `2.0.0`  
**Availability:** NuGet stable feed

**GA Changes:**
- Production-ready exporters
- Documentation complete
- Performance validated
- Breaking change policy enforced

---

## 4. Dependency Management

### 4.1 NuGet Dependencies (Final)

```xml
<ItemGroup>
  <!-- Core OpenTelemetry -->
  <PackageReference Include="OpenTelemetry.Api" Version="1.9.0" />
  <PackageReference Include="OpenTelemetry.Sdk" Version="1.9.0" />
  
  <!-- Optional exporters (users choose) -->
  <!-- <PackageReference Include="OpenTelemetry.Exporter.Console" Version="1.9.0" /> -->
  <!-- <PackageReference Include="OpenTelemetry.Exporter.Jaeger" Version="1.9.0" /> -->
  <!-- <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.9.0" /> -->
  <!-- <PackageReference Include="Azure.Monitor.OpenTelemetry" Version="1.0.0" /> -->
</ItemGroup>

<ItemGroup>
  <!-- Test dependencies -->
  <PackageReference Include="xunit" Version="2.7.0" Condition="'$(Configuration)' == 'Debug'" />
  <PackageReference Include="Moq" Version="4.20.0" Condition="'$(Configuration)' == 'Debug'" />
</ItemGroup>
```

### 4.2 Compatibility

**Supports:**
- .NET 8.0 LTS
- .NET 10.0 (preview/release)
- All platforms (Windows, Linux, macOS)

**Not supported:**
- .NET Framework (3.5, 4.x)
- .NET Core 3.1 (EOL)

---

## 5. Production Hardening Checklist

### 5.1 Pre-Release Verification

- [ ] All 40+ unit tests pass
- [ ] Integration tests pass with real ONNX models
- [ ] Performance overhead <2% verified by benchmarks
- [ ] Code coverage >85%
- [ ] No compiler warnings on net8.0 and net10.0
- [ ] Nullable reference types strict (no #nullable disable)
- [ ] XML documentation 100% for public APIs
- [ ] No hardcoded secrets or credentials
- [ ] Path traversal security verified
- [ ] Exception handling complete (no swallowed exceptions)

### 5.2 Documentation Checklist

- [ ] README.md updated with OTel section
- [ ] phase2-otel-examples.md complete with copy-paste code
- [ ] Jaeger integration guide
- [ ] Datadog integration guide
- [ ] Azure Monitor integration guide
- [ ] Troubleshooting guide
- [ ] Performance tuning guide
- [ ] Breaking change policy documented (zero breaking changes)

### 5.3 Build Pipeline Checklist

- [ ] Solution builds on CI (GitHub Actions)
- [ ] All tests run on CI (net8.0, net10.0)
- [ ] NuGet package created locally and tested
- [ ] Documentation built and validates
- [ ] Code analysis (StyleCop) passes
- [ ] No security warnings

### 5.4 Deployment Checklist

- [ ] NuGet package approved for beta
- [ ] GitHub release created with notes
- [ ] Documentation published to GitHub Pages
- [ ] Social media announcement ready
- [ ] Feedback collection process active

---

## 6. Risk Mitigation

### 6.1 Potential Risks

**Risk 1: Performance Regression**
- **Impact:** High
- **Mitigation:** Benchmark before release, sample high-volume operations
- **Rollback:** Disable via `AddLocalEmbeddingsOpenTelemetry()` registration

**Risk 2: Span Attribute Explosion**
- **Impact:** Medium
- **Mitigation:** Use semantic conventions, limit to <20 attributes per span
- **Rollback:** Disable detailed events via `EnableDetailedEvents=false`

**Risk 3: Breaking Change Accidentally Introduced**
- **Impact:** Critical
- **Mitigation:** Code review, verify core library untouched, test backward compatibility
- **Rollback:** Revert changes, release hotfix

**Risk 4: Memory Leak in Activity Disposal**
- **Impact:** High
- **Mitigation:** Use `using` statements, finalize blocks, integrate tests
- **Rollback:** Fix disposal, re-release beta

---

## 7. Success Criteria

✅ **Week 1:** Core infrastructure and metrics in place, first span implemented  
✅ **Week 2:** Decorator pattern working, GenerateEmbeddings instrumented, 20+ tests passing  
✅ **Week 3:** Streaming and baggage propagation complete, <2% overhead verified  
✅ **Week 4:** Full documentation, examples, beta release ready  

**Overall:** Zero breaking changes, <2% overhead, enterprise-ready, all exporters functional.

---

## Next Document

See **phase2-otel-examples.md** for production-ready code examples and integration samples.
