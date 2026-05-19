namespace ElBruno.LocalEmbeddings.OpenTelemetry.Internal;

/// <summary>
/// Provides access to Activity baggage for OpenTelemetry instrumentation.
/// </summary>
internal interface IActivityBaggageProvider
{
    /// <summary>
    /// Gets current baggage items.
    /// </summary>
    /// <returns>The current baggage entries.</returns>
    IEnumerable<KeyValuePair<string, string?>> GetBaggage();

    /// <summary>
    /// Sets a baggage value.
    /// </summary>
    /// <param name="key">Baggage key.</param>
    /// <param name="value">Baggage value.</param>
    void SetBaggage(string key, string? value);

    /// <summary>
    /// Parses and sets baggage values from a W3C baggage header string.
    /// </summary>
    /// <param name="baggageHeader">The raw baggage header value.</param>
    /// <returns><see langword="true"/> when at least one item is parsed; otherwise <see langword="false"/>.</returns>
    bool TryReadFromHeader(string? baggageHeader);
}
