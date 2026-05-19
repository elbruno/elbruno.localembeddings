using System.Diagnostics;
using ElBruno.LocalEmbeddings.OpenTelemetry.Options;

namespace ElBruno.LocalEmbeddings.OpenTelemetry.Internal;

/// <summary>
/// Helpers for W3C baggage parsing and activity tag attachment.
/// </summary>
internal static class BaggageExtensions
{
    /// <summary>
    /// Parses and writes baggage values from a W3C baggage header value.
    /// </summary>
    /// <param name="baggageHeader">Raw baggage header text.</param>
    /// <param name="provider">Optional baggage provider.</param>
    /// <returns><see langword="true"/> when at least one item is parsed; otherwise <see langword="false"/>.</returns>
    public static bool TryReadFromHeader(string? baggageHeader, IActivityBaggageProvider? provider = null)
    {
        if (string.IsNullOrWhiteSpace(baggageHeader))
        {
            return false;
        }

        provider ??= new ActivityBaggageProvider();

        var items = baggageHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        int parsedCount = 0;

        foreach (var item in items)
        {
            int equalsIndex = item.IndexOf('=');
            if (equalsIndex <= 0)
            {
                continue;
            }

            string encodedKey = item[..equalsIndex].Trim();
            string encodedValueAndMetadata = item[(equalsIndex + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(encodedKey))
            {
                continue;
            }

            int metadataSeparator = encodedValueAndMetadata.IndexOf(';');
            string encodedValue = metadataSeparator >= 0
                ? encodedValueAndMetadata[..metadataSeparator].Trim()
                : encodedValueAndMetadata.Trim();

            string key = Uri.UnescapeDataString(encodedKey);
            string value = Uri.UnescapeDataString(encodedValue);

            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            provider.SetBaggage(key, value);
            parsedCount++;
        }

        return parsedCount > 0;
    }

    /// <summary>
    /// Attaches baggage values as activity tags when enabled.
    /// </summary>
    /// <param name="activity">The target activity.</param>
    /// <param name="options">Telemetry options.</param>
    /// <param name="provider">Optional baggage provider.</param>
    public static void AttachBaggageToActivity(
        Activity activity,
        LocalEmbeddingsOpenTelemetryOptions options,
        IActivityBaggageProvider? provider = null)
    {
        ArgumentNullException.ThrowIfNull(activity);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.EnableBaggagePropagation || !options.RecordBaggageInAttributes)
        {
            return;
        }

        provider ??= new ActivityBaggageProvider();
        int maxItems = options.MaxBaggageItemsToRecord;
        if (maxItems == 0)
        {
            return;
        }

        var mergedBaggage = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var item in provider.GetBaggage())
        {
            if (string.IsNullOrWhiteSpace(item.Key))
            {
                continue;
            }

            mergedBaggage[item.Key] = item.Value;
            if (mergedBaggage.Count >= maxItems)
            {
                break;
            }
        }

        if (mergedBaggage.Count < maxItems)
        {
            foreach (var item in options.BaggageItems)
            {
                if (string.IsNullOrWhiteSpace(item.Key))
                {
                    continue;
                }

                mergedBaggage[item.Key] = item.Value;
                if (mergedBaggage.Count >= maxItems)
                {
                    break;
                }
            }
        }

        foreach (var item in mergedBaggage)
        {
            activity.SetTag($"{ActivityTags.BaggagePrefix}{item.Key}", item.Value);
        }
    }
}
