using System.Diagnostics;

namespace ElBruno.LocalEmbeddings;

/// <summary>
/// Profiles inference latency and memory to select optimal batch size.
/// </summary>
internal sealed class BatchSizeAutoTuner
{
    private const double DiminishingReturnsThreshold = 0.10; // 10% throughput improvement
    private const int WarmupRuns = 2;
    private const int MeasurementRuns = 3;

    /// <summary>
    /// Determines the optimal batch size by profiling inference performance.
    /// </summary>
    /// <param name="minBatch">Minimum batch size to consider.</param>
    /// <param name="maxBatch">Maximum batch size to consider.</param>
    /// <param name="runBatch">Function that executes inference for a given batch size and returns the elapsed time.</param>
    /// <returns>The optimal batch size that balances throughput and resource usage.</returns>
    /// <remarks>
    /// The algorithm:
    /// <list type="number">
    /// <item>Starts with minBatch and measures throughput (items/second)</item>
    /// <item>Doubles batch size and measures again</item>
    /// <item>Continues while doubling provides >10% throughput improvement</item>
    /// <item>Monitors GC pressure; backs off if Gen2 collections increase significantly</item>
    /// <item>Returns the batch size with best throughput before diminishing returns</item>
    /// </list>
    /// </remarks>
    public int DetermineBatchSize(int minBatch, int maxBatch, Func<int, TimeSpan> runBatch)
    {
        ArgumentNullException.ThrowIfNull(runBatch);
        ArgumentOutOfRangeException.ThrowIfLessThan(minBatch, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxBatch, minBatch);

        // Warmup
        for (var i = 0; i < WarmupRuns; i++)
        {
            runBatch(minBatch);
        }

        var currentBatch = minBatch;
        var bestBatch = minBatch;
        var bestThroughput = MeasureThroughput(currentBatch, runBatch);
        var previousThroughput = bestThroughput;

        while (currentBatch < maxBatch)
        {
            var nextBatch = Math.Min(currentBatch * 2, maxBatch);
            var gen2Before = GC.CollectionCount(2);
            
            var nextThroughput = MeasureThroughput(nextBatch, runBatch);
            
            var gen2After = GC.CollectionCount(2);
            var gen2Increase = gen2After - gen2Before;

            // Calculate throughput improvement ratio
            var improvement = (nextThroughput - previousThroughput) / previousThroughput;

            // Check for diminishing returns
            if (improvement < DiminishingReturnsThreshold)
            {
                // Less than 10% improvement — not worth the larger batch
                break;
            }

            // Check for excessive GC pressure (more than 2 Gen2 collections in measurement)
            if (gen2Increase > 2)
            {
                // Memory pressure is too high — stick with current batch
                break;
            }

            // This batch size is better
            bestBatch = nextBatch;
            bestThroughput = nextThroughput;
            previousThroughput = nextThroughput;
            currentBatch = nextBatch;
        }

        return bestBatch;
    }

    private static double MeasureThroughput(int batchSize, Func<int, TimeSpan> runBatch)
    {
        var totalTime = TimeSpan.Zero;

        for (var i = 0; i < MeasurementRuns; i++)
        {
            totalTime += runBatch(batchSize);
        }

        var avgTime = totalTime / MeasurementRuns;
        
        // Throughput = items per second
        return batchSize / avgTime.TotalSeconds;
    }
}
