using System.Diagnostics;

namespace ElBruno.LocalEmbeddings.OpenTelemetry.Internal;

/// <summary>
/// ActivitySource for OpenTelemetry instrumentation of ElBruno.LocalEmbeddings.
/// </summary>
internal static class OpenTelemetryActivitySource
{
    /// <summary>
    /// Gets the ActivitySource for ElBruno.LocalEmbeddings operations.
    /// </summary>
    public static readonly ActivitySource Source = new ActivitySource("ElBruno.LocalEmbeddings", "1.0.0");

    // Activity names (operation identifiers)
    public const string GenerateEmbeddings = "ElBruno.LocalEmbeddings.GenerateEmbeddings";
    public const string LoadModel = "ElBruno.LocalEmbeddings.LoadModel";
    public const string BatchGenerate = "ElBruno.LocalEmbeddings.BatchGenerate";
    public const string StreamingGenerate = "ElBruno.LocalEmbeddings.StreamingGenerate";
    public const string StreamBuffer = "ElBruno.LocalEmbeddings.StreamBuffer";
    public const string StreamYield = "ElBruno.LocalEmbeddings.StreamYield";
    public const string DownloadModel = "ElBruno.LocalEmbeddings.DownloadModel";
    public const string ValidateCache = "ElBruno.LocalEmbeddings.ValidateCache";
    public const string VectorSearch = "ElBruno.LocalEmbeddings.VectorSearch";
    public const string ApplyQuantization = "ElBruno.LocalEmbeddings.ApplyQuantization";
    public const string PostProcessing = "ElBruno.LocalEmbeddings.PostProcessing";
}
