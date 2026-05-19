using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ElBruno.LocalEmbeddings.Tests.Phase2.Fixtures;

/// <summary>
/// Fixture for OpenTelemetry testing with in-memory exporters.
/// Records activities and metrics for validation in tests.
/// Implements IAsyncLifetime for proper resource management.
/// </summary>
public class TelemetryFixture : IAsyncLifetime
{
    private ActivityListener? _activityListener;
    private readonly List<Activity> _recordedActivities = new();
    private readonly Dictionary<string, int> _metricValues = new();

    public IReadOnlyList<Activity> RecordedActivities => _recordedActivities.AsReadOnly();
    public IReadOnlyDictionary<string, int> MetricValues => _metricValues;

    public async Task InitializeAsync()
    {
        // Set up activity listener for capturing traces
        _activityListener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };

        _activityListener.ActivityStarted += (activity) =>
        {
            _recordedActivities.Add(activity);
        };

        ActivitySource.AddActivityListener(_activityListener);

        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _activityListener?.Dispose();
        _recordedActivities.Clear();
        _metricValues.Clear();

        await Task.CompletedTask;
    }

    /// <summary>
    /// Records a metric value (counter, gauge, or histogram value).
    /// </summary>
    public void RecordMetric(string metricName, int value)
    {
        _metricValues[metricName] = value;
    }

    /// <summary>
    /// Increments a counter metric.
    /// </summary>
    public void IncrementCounter(string metricName, int delta = 1)
    {
        if (_metricValues.TryGetValue(metricName, out var current))
        {
            _metricValues[metricName] = current + delta;
        }
        else
        {
            _metricValues[metricName] = delta;
        }
    }

    /// <summary>
    /// Gets all activities with a specific operation name.
    /// </summary>
    public List<Activity> GetActivitiesByName(string operationName)
    {
        var result = new List<Activity>();
        foreach (var activity in _recordedActivities)
        {
            if (activity.OperationName == operationName)
                result.Add(activity);
        }
        return result;
    }

    /// <summary>
    /// Gets an activity tag value by name.
    /// Returns null if activity not found or tag not present.
    /// </summary>
    public object? GetActivityTag(Activity activity, string tagName)
    {
        foreach (var tag in activity.Tags)
        {
            if (tag.Key == tagName)
                return tag.Value;
        }
        return null;
    }

    /// <summary>
    /// Clears all recorded activities and metrics.
    /// Useful for test isolation.
    /// </summary>
    public void Clear()
    {
        _recordedActivities.Clear();
        _metricValues.Clear();
    }

    /// <summary>
    /// Asserts that an activity was recorded with expected properties.
    /// </summary>
    public Activity? FindActivityByName(string operationName)
    {
        foreach (var activity in _recordedActivities)
        {
            if (activity.OperationName == operationName)
                return activity;
        }
        return null;
    }

    /// <summary>
    /// Gets the count of recorded activities.
    /// </summary>
    public int GetActivityCount() => _recordedActivities.Count;

    /// <summary>
    /// Gets the count of recorded activities by operation name.
    /// </summary>
    public int GetActivityCountByName(string operationName)
    {
        int count = 0;
        foreach (var activity in _recordedActivities)
        {
            if (activity.OperationName == operationName)
                count++;
        }
        return count;
    }
}
