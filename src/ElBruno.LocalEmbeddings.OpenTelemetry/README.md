# ElBruno.LocalEmbeddings.OpenTelemetry

OpenTelemetry observability instrumentation for **ElBruno.LocalEmbeddings** — distributed tracing, metrics collection, and structured events for enterprise observability.

## Overview

This package adds comprehensive OpenTelemetry instrumentation to ElBruno.LocalEmbeddings, enabling:

- **Distributed tracing** with W3C TraceContext propagation
- **Metrics collection** (latency histograms, throughput counters, error rates)
- **Structured events** for root cause analysis
- **Enterprise backends** support: Jaeger, Datadog, Azure Monitor, Prometheus, Grafana
- **Zero breaking changes** — opt-in instrumentation via dependency injection
- **<2% performance overhead** — minimal cost for full telemetry stack

## Quick Start

### 1. Install NuGet Package

```bash
dotnet add package ElBruno.LocalEmbeddings.OpenTelemetry
```

### 2. Register Instrumentation

```csharp
var services = new ServiceCollection();

services
    .AddLocalEmbeddings()
    .AddLocalEmbeddingsOpenTelemetry(options =>
    {
        options.EnableTracing = true;
        options.EnableMetrics = true;
        options.SamplingRate = 1.0; // 100% sampling
    });
```

### 3. Configure Exporter (Jaeger Example)

```csharp
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Instrumentation;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var tracerProvider = new TracerProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("my-app"))
    .AddSource("ElBruno.LocalEmbeddings")
    .AddJaegerExporter(options =>
    {
        options.AgentHost = "localhost";
        options.AgentPort = 6831;
    })
    .Build();
```

## Configuration Options

### LocalEmbeddingsOpenTelemetryOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EnableTracing` | `bool` | `true` | Enable OpenTelemetry tracing |
| `EnableMetrics` | `bool` | `true` | Enable metrics collection |
| `EnableBaggagePropagation` | `bool` | `false` | Enable W3C baggage propagation |
| `EnableBaggage` | `bool` | `false` | Alias for `EnableBaggagePropagation` |
| `SamplingRate` | `double` | `1.0` | Sampling rate (0.0 - 1.0) |
| `RecordExceptionDetails` | `bool` | `true` | Record exception details in spans |
| `RecordBaggageInAttributes` | `bool` | `false` | Record baggage items in span attributes |
| `BaggageItems` | `IDictionary<string, string>` | `{}` | Custom baggage entries appended to span tags |
| `MaxBaggageItemsToRecord` | `int` | `16` | Safety cap for baggage tags attached per span |

## Trace Operations

The following operations are instrumented with distributed tracing:

| Operation | Activity Name | Duration |
|-----------|---------------|----------|
| Embedding Generation | `ElBruno.LocalEmbeddings.GenerateEmbeddings` | 5ms - 500ms |
| Model Loading | `ElBruno.LocalEmbeddings.LoadModel` | 100ms - 5s |
| Batch Processing | `ElBruno.LocalEmbeddings.BatchGenerate` | 1ms - 100ms |
| Streaming Generation | `ElBruno.LocalEmbeddings.StreamingGenerate` | 10ms - 1s |
| Stream Buffering | `ElBruno.LocalEmbeddings.StreamBuffer` | 0.5ms - 50ms |
| Vector Search | `ElBruno.LocalEmbeddings.VectorSearch` | 0.1ms - 50ms |
| Model Download | `ElBruno.LocalEmbeddings.DownloadModel` | 500ms - 30s |
| Cache Validation | `ElBruno.LocalEmbeddings.ValidateCache` | 1ms - 50ms |

## Metrics

### Histograms (Latency)
- `embedding.latency.ms` — Embedding generation latency
- `model.load.latency.ms` — Model loading latency
- `quantization.check.latency.ms` — Quantization check latency
- `batch.size.distribution` — Batch size distribution

### Counters (Cumulative)
- `embeddings.generated.total` — Total embeddings generated
- `models.loaded.total` — Total models loaded
- `errors.total` — Total errors
- `cache.hits.total` — Total cache hits
- `cache.misses.total` — Total cache misses

### Gauges (Point-in-time)
- `active.requests` — Active embedding requests
- `model.cache.size.mb` — Model cache size in MB

## Enterprise Backends

### Jaeger

```csharp
var tracerProvider = new TracerProviderBuilder()
    .AddSource("ElBruno.LocalEmbeddings")
    .AddJaegerExporter(options =>
    {
        options.AgentHost = "localhost";
        options.AgentPort = 6831;
    })
    .Build();
```

### Datadog

```csharp
var tracerProvider = new TracerProviderBuilder()
    .AddSource("ElBruno.LocalEmbeddings")
    .AddOtlpExporter(options =>
    {
        options.Endpoint = new Uri("http://localhost:4317");
    })
    .Build();
```

### Azure Monitor

```csharp
var tracerProvider = new TracerProviderBuilder()
    .AddSource("ElBruno.LocalEmbeddings")
    .AddAzureMonitorExporter(options =>
    {
        options.ConnectionString = "InstrumentationKey=...";
    })
    .Build();
```

### Prometheus

```csharp
var meterProvider = new MeterProviderBuilder()
    .AddMeter("ElBruno.LocalEmbeddings")
    .AddPrometheusExporter()
    .Build();
```

## Performance Target

- **Overhead:** <2% (Activity + Metrics collection combined)
- **Latency impact:** <1ms per operation
- **Memory impact:** <5MB cache for metrics

## License

MIT — See LICENSE file for details.

## Contributing

Contributions are welcome! Please follow the [Contributing Guidelines](https://github.com/elbruno/elbruno.localembeddings/blob/main/docs/contributing.md).
