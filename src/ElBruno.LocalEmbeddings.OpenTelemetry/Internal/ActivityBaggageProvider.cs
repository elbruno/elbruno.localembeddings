using System.Diagnostics;

namespace ElBruno.LocalEmbeddings.OpenTelemetry.Internal;

/// <summary>
/// Default activity baggage provider based on <see cref="Activity.Current"/>.
/// </summary>
internal sealed class ActivityBaggageProvider : IActivityBaggageProvider
{
    /// <inheritdoc/>
    public IEnumerable<KeyValuePair<string, string?>> GetBaggage() =>
        Activity.Current?.Baggage ?? [];

    /// <inheritdoc/>
    public void SetBaggage(string key, string? value)
    {
        if (value is null)
        {
            return;
        }

        Activity.Current?.AddBaggage(key, value);
    }

    /// <inheritdoc/>
    public bool TryReadFromHeader(string? baggageHeader) => BaggageExtensions.TryReadFromHeader(baggageHeader, this);
}
