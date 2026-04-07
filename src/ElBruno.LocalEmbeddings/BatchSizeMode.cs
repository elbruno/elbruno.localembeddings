namespace ElBruno.LocalEmbeddings;

/// <summary>
/// Specifies the batch size strategy for embedding generation.
/// </summary>
public enum BatchSizeMode
{
    /// <summary>
    /// Use a fixed batch size specified in options.
    /// </summary>
    Fixed,

    /// <summary>
    /// Automatically tune batch size based on runtime profiling.
    /// Determines optimal batch size by measuring inference latency and memory pressure.
    /// </summary>
    Auto
}
