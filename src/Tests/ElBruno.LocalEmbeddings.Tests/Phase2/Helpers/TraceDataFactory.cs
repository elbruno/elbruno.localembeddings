using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ElBruno.LocalEmbeddings.Tests.Phase2.Helpers;

/// <summary>
/// Factory for generating mock telemetry data for OpenTelemetry tests.
/// Creates activities, events, and metric data for validation.
/// </summary>
public static class TraceDataFactory
{
    /// <summary>
    /// Creates a mock activity (span) with standard attributes for embedding generation.
    /// </summary>
    public static Activity CreateEmbeddingGenerationActivity(
        string modelName = "all-minilm-l6-v2",
        int vectorCount = 100,
        long latencyMs = 250)
    {
        var activity = new Activity("generate.embeddings");
        activity.Start();

        activity.SetTag("model.name", modelName);
        activity.SetTag("embedding.count", vectorCount);
        activity.SetTag("latency_ms", latencyMs);
        activity.SetTag("batch_size", 32);
        activity.SetTag("execution_provider", "cpu");

        return activity;
    }

    /// <summary>
    /// Creates a mock activity for model loading.
    /// </summary>
    public static Activity CreateModelLoadActivity(
        string modelName = "all-minilm-l6-v2",
        long latencyMs = 1000,
        long modelSizeBytes = 500_000_000)
    {
        var activity = new Activity("model.load");
        activity.Start();

        activity.SetTag("model.name", modelName);
        activity.SetTag("latency_ms", latencyMs);
        activity.SetTag("model_size_bytes", modelSizeBytes);
        activity.SetTag("cache_hit", false);

        return activity;
    }

    /// <summary>
    /// Creates a mock activity with an error event.
    /// </summary>
    public static Activity CreateErrorActivity(
        string operationName = "generate.embeddings",
        string errorMessage = "Model not found",
        string stackTrace = "at ElBruno.LocalEmbeddings.OnnxEmbeddingModel.LoadAsync()")
    {
        var activity = new Activity(operationName);
        activity.Start();

        activity.SetTag("error.type", typeof(InvalidOperationException).Name);
        activity.SetTag("error.message", errorMessage);
        
        var eventTags = new ActivityTagsCollection
        {
            { "exception.message", errorMessage },
            { "exception.stacktrace", stackTrace },
        };
        activity.AddEvent(new ActivityEvent("exception", default, eventTags));

        return activity;
    }

    /// <summary>
    /// Generates test metric values for validation.
    /// Returns dictionary of metric name to value.
    /// </summary>
    public static Dictionary<string, long> GenerateMetricValues()
    {
        return new Dictionary<string, long>
        {
            { "embedding.generation.count", 1000 },
            { "embedding.generation.latency.sum", 250_000 },    // 250 seconds total
            { "embedding.generation.latency.count", 1000 },     // 1000 operations
            { "model.load.duration_ms", 1000 },
            { "model.cache.hits", 5 },
            { "model.cache.misses", 1 },
        };
    }

    /// <summary>
    /// Generates structured log entries for validation.
    /// Returns list of tuples: (timestamp, level, message, properties)
    /// </summary>
    public static List<(DateTime Timestamp, string Level, string Message, Dictionary<string, object>)> GenerateStructuredLogs()
    {
        var logs = new List<(DateTime, string, string, Dictionary<string, object>)>();

        logs.Add((
            DateTime.UtcNow,
            "Information",
            "Model loaded successfully",
            new Dictionary<string, object>
            {
                { "model_name", "all-minilm-l6-v2" },
                { "latency_ms", 1000 },
                { "model_size_mb", 500 }
            }
        ));

        logs.Add((
            DateTime.UtcNow,
            "Information",
            "Embeddings generated",
            new Dictionary<string, object>
            {
                { "embedding_count", 100 },
                { "latency_ms", 250 },
                { "batch_size", 32 }
            }
        ));

        return logs;
    }

    /// <summary>
    /// Generates parent-child activity relationship for testing span hierarchy.
    /// </summary>
    public static (Activity Parent, Activity Child) CreateParentChildActivityPair()
    {
        var parent = new Activity("model.initialize");
        parent.Start();

        var child = new Activity("model.load");
        child.Start();
        
        var parentId = parent.Id;
        if (parentId != null)
        {
            child.SetParentId(parentId);
        }

        return (parent, child);
    }

    /// <summary>
    /// Creates a batch of activities simulating multiple embedding operations.
    /// </summary>
    public static List<Activity> CreateBatchActivities(int count)
    {
        var activities = new List<Activity>(count);
        
        for (int i = 0; i < count; i++)
        {
            var activity = new Activity($"generate.embeddings.{i}");
            activity.Start();
            activity.SetTag("batch_index", i);
            activity.SetTag("vector_count", 100 + (i * 10));
            activity.SetTag("latency_ms", 200 + (i * 5));
            activities.Add(activity);
        }

        return activities;
    }

    /// <summary>
    /// Generates baggage items for W3C trace context propagation.
    /// </summary>
    public static Dictionary<string, string> GenerateBaggageItems()
    {
        return new Dictionary<string, string>
        {
            { "tenant_id", "acme-corp" },
            { "request_id", "req-12345" },
            { "user_id", "user-67890" },
            { "trace_parent", "00-12345678901234567890123456789012-1234567890123456-01" }
        };
    }

    /// <summary>
    /// Generates W3C Trace Context header for testing propagation.
    /// </summary>
    public static string GenerateW3CTraceContext()
    {
        return "00-12345678901234567890123456789012-1234567890123456-01";
    }
}
