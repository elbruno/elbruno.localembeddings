# PHASE 2: OPENTELEMETRY INTEGRATION — PRODUCTION-READY CODE EXAMPLES

**Prepared by:** Kane (Integration Specialist)  
**Date:** 2026-05-26  
**Status:** ✅ Examples Ready for Implementation

---

## Executive Summary

This document provides **copy-paste code examples** for integrating OpenTelemetry observability into ElBruno.LocalEmbeddings applications. Examples cover all major exporter backends (Console, Jaeger, Datadog, Azure Monitor) and troubleshooting patterns.

---

## 1. Basic Setup (Console Exporter — Development)

### 1.1 Program.cs Configuration

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.AI;
using ElBruno.LocalEmbeddings;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // 1. Register core embedding generator
        services.AddLocalEmbeddings(opts =>
        {
            opts.ModelName = "sentence-transformers/all-MiniLM-L6-v2";
            opts.PreferQuantized = true;  // Use int8 model if available
            opts.MaxSequenceLength = 512;
        });
        
        // 2. Register OpenTelemetry instrumentation
        services.AddLocalEmbeddingsOpenTelemetry(opts =>
        {
            opts.EnableTracing = true;
            opts.EnableMetrics = true;
            opts.EnableBaggage = true;
            opts.SamplingRate = 1.0;  // Sample all traces (for dev)
            opts.EnableDetailedEvents = true;  // Include batch timing events
        });
        
        // 3. Configure OpenTelemetry exporters
        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(
                serviceName: "embedding-api",
                serviceVersion: "1.0.0"))
            
            // Tracing configuration
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()  // For ASP.NET Core requests
                .AddHttpClientInstrumentation()  // For HTTP calls
                .AddConsoleExporter(opts =>
                {
                    opts.Targets = ConsoleExporterOutputTargets.Display;
                })
            )
            
            // Metrics configuration
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddConsoleExporter()
            );
    })
    .ConfigureLogging((context, logging) =>
    {
        logging.AddOpenTelemetry(opts => opts
            .AddConsoleExporter());
    });

var host = builder.Build();
await host.RunAsync();
```

### 1.2 Console Output Example

```
Activity.ElBruno.LocalEmbeddings.GenerateEmbeddings
(c2f7c1e2e9b0a3d4e5f6a7b8c9d0e1f2, 0000000000000001):
{
  SpanId: e5f6a7b8c9d0e1f2,
  TraceId: c2f7c1e2e9b0a3d4e5f6a7b8c9d0e1f2,
  TraceFlags: Recorded,
  TraceState: '',
  Parent: (external),
  Kind: Internal,
  StartTime: 2026-05-26T10:15:30.1234567Z,
  Duration: 00:00:00.0872145,
  Status: Ok,
  Tags: {
    llm.system: local-embeddings,
    llm.request.model: sentence-transformers/all-MiniLM-L6-v2,
    llm.usage.input_tokens: 500,
    llm.usage.output_dimension: 384,
    llm.quantization_format: int8,
    custom.batch_size_actual: 32,
    custom.cache_status: hit
  }
}
```

---

## 2. Jaeger Integration (Distributed Tracing)

### 2.1 Docker Compose: Jaeger Setup

```yaml
# docker-compose.yml
version: '3.8'

services:
  jaeger:
    image: jaegertracing/all-in-one:latest
    ports:
      - "16686:16686"  # Jaeger UI
      - "14268:14268"  # Collector HTTP
      - "6831:6831/udp"  # Agent compact thrift
    environment:
      - COLLECTOR_ZIPKIN_HOST_PORT=:9411
      - COLLECTOR_OTLP_ENABLED=true
    command: >
      --memory.max-traces=10000
      --collector.otlp.enabled=true
      --collector.grpc.host-port=:4317

  # Optional: sample application
  embedding-service:
    build: .
    ports:
      - "5000:5000"
    environment:
      - OTEL_EXPORTER_OTLP_ENDPOINT=http://jaeger:4317
    depends_on:
      - jaeger
```

### 2.2 Program.cs with Jaeger Exporter

```csharp
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddLocalEmbeddings();
        services.AddLocalEmbeddingsOpenTelemetry();
        
        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("embedding-api"))
            
            // Jaeger OTLP exporter
            .WithTracing(tracing => tracing
                .AddOtlpExporter(opts =>
                {
                    // Option 1: Via environment variable
                    opts.Endpoint = new Uri(
                        Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
                        ?? "http://localhost:4317");
                    
                    // Option 2: Explicit configuration
                    opts.Protocol = OtlpExportProtocol.Grpc;
                    opts.TimeoutMilliseconds = 10000;
                })
            );
    });

var host = builder.Build();
await host.RunAsync();
```

### 2.3 Querying Jaeger UI

**URL:** `http://localhost:16686`

**Find traces for embedding generation:**
1. Service: `embedding-api`
2. Operation: `ElBruno.LocalEmbeddings.GenerateEmbeddings`
3. Tags filter: `llm.quantization_format=int8`
4. Latency: >100ms

**Example query (Jaeger DSL):**
```
service.name=embedding-api AND operation_name=ElBruno.LocalEmbeddings.GenerateEmbeddings
```

---

## 3. Datadog APM Integration

### 3.1 Docker Compose: Datadog Agent Setup

```yaml
# docker-compose.yml
version: '3.8'

services:
  datadog-agent:
    image: datadog/agent:latest
    ports:
      - "8126:8126"  # APM Agent
      - "8125:8125/udp"  # StatsD
    environment:
      - DD_API_KEY=${DATADOG_API_KEY}
      - DD_SITE=datadoghq.com
      - DD_APM_ENABLED=true
      - DD_LOGS_ENABLED=true
      - DD_METRICS_ENABLED=true
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro

  embedding-service:
    build: .
    ports:
      - "5000:5000"
    environment:
      - OTEL_EXPORTER_OTLP_ENDPOINT=http://datadog-agent:4317
      - DD_TRACE_AGENT_PORT=8126
    depends_on:
      - datadog-agent
```

### 3.2 Program.cs with Datadog Exporter

```csharp
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddLocalEmbeddings();
        services.AddLocalEmbeddingsOpenTelemetry(opts =>
        {
            opts.EnableTracing = true;
            opts.EnableMetrics = true;
            opts.SamplingRate = 0.1;  // Sample 10% to reduce Datadog costs
        });
        
        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(
                serviceName: "embedding-api",
                serviceVersion: "1.0.0"))
            
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(opts =>
                {
                    // Datadog OTLP endpoint
                    opts.Endpoint = new Uri(
                        Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
                        ?? "http://localhost:4317");
                    opts.Protocol = OtlpExportProtocol.Grpc;
                    opts.TimeoutMilliseconds = 5000;
                })
            )
            
            .WithMetrics(metrics => metrics
                .AddMeter("ElBruno.LocalEmbeddings")
                .AddOtlpExporter(opts =>
                {
                    opts.Endpoint = new Uri(
                        Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
                        ?? "http://localhost:4317");
                })
            );
    });

var host = builder.Build();
await host.RunAsync();
```

### 3.3 Datadog Dashboard: Embedding Latency

```sql
# Datadog monitor query for P95 embedding latency
avg:trace.otel.system{span_name:"ElBruno.LocalEmbeddings.GenerateEmbeddings"}.as_count() > 100
```

---

## 4. Azure Monitor Integration

### 4.1 Azure Setup

```bash
# Create Application Insights resource
az monitor app-insights component create \
  --app "embedding-api" \
  --location "eastus" \
  --resource-group "myResourceGroup"

# Get connection string
az monitor app-insights component show \
  --app "embedding-api" \
  --resource-group "myResourceGroup" \
  --query connectionString -o tsv
```

### 4.2 Program.cs with Azure Monitor Exporter

```csharp
using Azure.Monitor.OpenTelemetry;
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "APPLICATIONINSIGHTS_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                "APPLICATIONINSIGHTS_CONNECTION_STRING not set");
        
        services.AddLocalEmbeddings();
        services.AddLocalEmbeddingsOpenTelemetry(opts =>
        {
            opts.EnableTracing = true;
            opts.EnableMetrics = true;
            opts.SamplingRate = 1.0;  // Send all traces to Azure Monitor
        });
        
        // Use Azure Monitor exporter
        var resourceBuilder = ResourceBuilder
            .CreateDefault()
            .AddService("embedding-api", serviceVersion: "1.0.0")
            .AddAttributes(new Dictionary<string, object>
            {
                { "environment", "production" },
                { "service.instance.id", Environment.MachineName }
            });
        
        services.AddOpenTelemetry()
            .ConfigureResource(_ => resourceBuilder)
            
            .UseAzureMonitor(opts =>
            {
                opts.ConnectionString = connectionString;
                opts.EnableActivityMetrics = true;
                opts.LogsExportEnabled = true;
                opts.TracesExportEnabled = true;
                opts.MetricsExportEnabled = true;
            });
    });

var host = builder.Build();
await host.RunAsync();
```

### 4.3 Azure Monitor Kusto Query

```kusto
// Query embedding generation traces
customDimensions.['ElBruno.LocalEmbeddings.GenerateEmbeddings']
| where name == "ElBruno.LocalEmbeddings.GenerateEmbeddings"
| project timestamp, duration, 
    model = tostring(customDimensions.['llm.request.model']),
    quantization = tostring(customDimensions.['llm.quantization_format']),
    cache_status = tostring(customDimensions.['llm.cache_status'])
| summarize 
    p95_duration = percentile(duration, 95),
    error_count = dcountif(name, name == "exception"),
    cache_hit_rate = sum(iff(cache_status == "hit", 1, 0)) / count()
    by model, quantization
```

---

## 5. Custom Application Code Examples

### 5.1 Basic Embedding Generation with OpenTelemetry

```csharp
public class EmbeddingService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingService(
        IEmbeddingGenerator<string, Embedding<float>> generator,
        ILogger<EmbeddingService> logger)
    {
        _generator = generator;
        _logger = logger;
    }

    public async Task<Dictionary<string, float[]>> EmbedDocumentsAsync(
        IEnumerable<string> documents,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting embedding generation for {DocumentCount} documents",
            documents.Count());

        try
        {
            // OpenTelemetry instrumentation is transparent
            var embeddings = await _generator.GenerateAsync(
                documents,
                cancellationToken: cancellationToken);

            var result = new Dictionary<string, float[]>();
            foreach (var (text, embedding) in documents.Zip(embeddings.Output))
            {
                result[text] = embedding.Vector.ToArray();
            }

            _logger.LogInformation("Successfully embedded {DocumentCount} documents", documents.Count());
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to embed documents");
            throw;
        }
    }
}
```

### 5.2 Streaming Embeddings with Progress Monitoring

```csharp
public class StreamingEmbeddingService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;

    public StreamingEmbeddingService(
        IEmbeddingGenerator<string, Embedding<float>> generator)
    {
        _generator = generator;
    }

    public async IAsyncEnumerable<(string Text, float[] Vector)> EmbedStreamAsync(
        IAsyncEnumerable<string> texts,
        IProgress<int>? progress = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var progress_impl = progress ?? new Progress<int>();
        int processedCount = 0;

        // Assuming StreamingExtensions.GenerateStreamingAsync available
        await foreach (var embedding in _generator.GenerateStreamingAsync(
            texts, 
            new StreamingEmbeddingOptions { BufferSize = 32 },
            cancellationToken))
        {
            progress_impl.Report(++processedCount);
            yield return (embedding.Metadata["text"] as string ?? "", 
                embedding.Vector.ToArray());
        }
    }
}
```

### 5.3 Vector Search with Similarity Scoring

```csharp
public class VectorSearchService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;

    public async Task<IEnumerable<(string Document, float Similarity)>> SearchAsync(
        string query,
        IEnumerable<string> corpus,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        // Generate query embedding (instrumented with OpenTelemetry)
        var queryEmbedding = await _generator.GenerateEmbeddingAsync(
            query, cancellationToken: cancellationToken);

        // Generate corpus embeddings
        var corpusEmbeddings = await _generator.GenerateAsync(
            corpus, cancellationToken: cancellationToken);

        // Find top-k similar documents using SIMD-accelerated similarity
        var results = corpusEmbeddings.Output
            .Zip(corpus)
            .Select(pair => (
                Document: pair.Second,
                Similarity: pair.First.CosineSimilarity(queryEmbedding)
            ))
            .OrderByDescending(x => x.Similarity)
            .Take(topK)
            .ToList();

        return results;
    }
}
```

---

## 6. Baggage & Correlation Context

### 6.1 Setting Up Request Context

```csharp
public class RequestContextMiddleware
{
    private readonly RequestDelegate _next;

    public RequestContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Extract or create trace correlation ID
        var correlationId = context.Request.Headers
            .FirstOrDefault(h => h.Key == "X-Correlation-ID").Value
            .FirstOrDefault() ?? Guid.NewGuid().ToString();

        // Extract tenant ID from claims or header
        var tenantId = context.User?.FindFirst("tenant_id")?.Value
            ?? context.Request.Headers["X-Tenant-ID"].FirstOrDefault()
            ?? "default";

        // Set W3C baggage for cross-service correlation
        var baggageItems = new[]
        {
            new KeyValuePair<string, string>("trace.correlation_id", correlationId),
            new KeyValuePair<string, string>("trace.tenant_id", tenantId),
            new KeyValuePair<string, string>("trace.user_id", 
                context.User?.FindFirst("sub")?.Value ?? "anonymous")
        };

        Baggage.SetBaggage(baggageItems);

        // Add to response for client correlation
        context.Response.Headers["X-Correlation-ID"] = correlationId;

        await _next(context);
    }
}

// Register middleware
app.UseMiddleware<RequestContextMiddleware>();
```

### 6.2 Reading Baggage in Custom Code

```csharp
public class BaggageAwareEmbeddingService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;
    private readonly ILogger<BaggageAwareEmbeddingService> _logger;

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default)
    {
        // Read baggage for correlation
        var correlationId = Baggage.GetBaggage()
            .FirstOrDefault(b => b.Key == "trace.correlation_id").Value
            ?? "unknown";
        
        var tenantId = Baggage.GetBaggage()
            .FirstOrDefault(b => b.Key == "trace.tenant_id").Value
            ?? "default";

        _logger.LogInformation(
            "Generating embeddings - CorrelationId: {CorrelationId}, TenantId: {TenantId}",
            correlationId, tenantId);

        return await _generator.GenerateAsync(texts, 
            cancellationToken: cancellationToken);
    }
}
```

---

## 7. Troubleshooting Guides

### 7.1 Traces Not Appearing in Jaeger

**Problem:** Jaeger UI shows no traces from embedding service.

**Diagnosis Steps:**

```csharp
// Step 1: Verify ActivitySource is active
var listener = new ActivityListener
{
    ShouldListenTo = _ => true,
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => 
        ActivitySamplingResult.AllData
};
ActivitySource.AddActivityListener(listener);
Debug.WriteLine("ActivityListener registered");

// Step 2: Check exporter endpoint
var otelEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
Debug.WriteLine($"OTLP Endpoint: {otelEndpoint}");

// Step 3: Verify connection to Jaeger
using var httpClient = new HttpClient();
var response = await httpClient.GetAsync("http://localhost:14268/api/traces");
Debug.WriteLine($"Jaeger health check: {response.StatusCode}");

// Step 4: Enable console exporter temporarily
.WithTracing(tracing => tracing.AddConsoleExporter())
```

**Solution:** Ensure Jaeger is running and OTLP endpoint is accessible.

### 7.2 High Memory Usage from Traces

**Problem:** Memory grows unbounded during streaming.

**Diagnosis:**

```csharp
// Check if activities are being disposed
using (var activity = ActivitySource.StartActivity("test"))
{
    Debug.WriteLine($"Activity created: {activity?.Id}");
    // Activity should be disposed after using block
}

// Monitor GC
var gcMemBefore = GC.GetTotalMemory(true);
// ... generate embeddings ...
var gcMemAfter = GC.GetTotalMemory(true);
Debug.WriteLine($"Memory increase: {(gcMemAfter - gcMemBefore) / 1024 / 1024} MB");
```

**Solution:** Use `using` statements consistently, disable `EnableDetailedEvents` for high-volume operations.

### 7.3 Sampling Configuration Errors

**Problem:** Traces are sampled unexpectedly.

**Configuration Check:**

```csharp
// Verify sampling rate
var options = new LocalEmbeddingsOpenTelemetryOptions
{
    SamplingRate = 1.0  // Must be 1.0 to sample all
};

// Alternative: Custom sampler
.WithTracing(tracing => tracing
    .SetSampler(new AlwaysOnSampler())  // Sample everything
    // or
    .SetSampler(new TraceIdRatioBasedSampler(0.1))  // Sample 10%
)
```

### 7.4 Performance Degradation

**Problem:** Application is slower with OpenTelemetry enabled.

**Performance Check:**

```csharp
var sw = Stopwatch.StartNew();
for (int i = 0; i < 1000; i++)
{
    await generator.GenerateAsync(["test text"]);
}
sw.Stop();

var avgMs = sw.Elapsed.TotalMilliseconds / 1000;
Console.WriteLine($"Average: {avgMs}ms");
// Should be <2% slower than baseline
```

**Solution:** Reduce `SamplingRate`, disable `EnableDetailedEvents`, use OTLP batching.

---

## 8. Production Checklist

Before deploying to production:

- [ ] Jaeger or similar backend configured and tested
- [ ] OTLP exporter endpoint verified accessible from production network
- [ ] Sampling rate appropriate (typically 0.01 - 0.1 for high-volume)
- [ ] Baggage propagation tested across services
- [ ] Alerts configured for high error rates
- [ ] Performance impact measured (<2% overhead)
- [ ] Log rotation configured for exported traces
- [ ] Secrets (connection strings) in secure vaults, not config files
- [ ] Network egress for telemetry unblocked by firewalls
- [ ] Monitoring of monitoring system (agent health checks)

---

## 9. Quick Reference

**Console Exporter (Development):**
```bash
# No setup needed, just enable Console exporter
.AddConsoleExporter()
```

**Jaeger (Local Testing):**
```bash
docker run -p 16686:16686 -p 14268:14268 jaegertracing/all-in-one:latest
# Then use OTLP exporter pointing to localhost:4317
```

**Datadog (Production):**
```bash
export DATADOG_API_KEY=<your-key>
# Use OTEL_EXPORTER_OTLP_ENDPOINT=http://agent:4317
```

**Azure Monitor (Production):**
```bash
export APPLICATIONINSIGHTS_CONNECTION_STRING=<your-connection-string>
# Use Azure Monitor exporter with UseAzureMonitor()
```

---

## End of Design Phase

All 5 design documents are now complete:

1. ✅ **phase2-opentelemetry-design.md** — Complete technical specification
2. ✅ **phase2-otel-trace-design.md** — Trace architecture and spans
3. ✅ **phase2-otel-metrics-design.md** — Metrics and Prometheus integration
4. ✅ **phase2-otel-implementation-guide.md** — Week-by-week roadmap
5. ✅ **phase2-otel-examples.md** — Production-ready code examples

**Ready for implementation** by the engineering squad.
