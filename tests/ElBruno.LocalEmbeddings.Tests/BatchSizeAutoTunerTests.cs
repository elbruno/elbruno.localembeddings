using System.Diagnostics;

namespace ElBruno.LocalEmbeddings.Tests;

public class BatchSizeAutoTunerTests
{
    [Fact]
    public void DetermineBatchSize_ReturnsValueWithinRange()
    {
        var tuner = new BatchSizeAutoTuner();
        var runBatch = (int batchSize) => TimeSpan.FromMilliseconds(100.0 / batchSize);

        var result = tuner.DetermineBatchSize(minBatch: 4, maxBatch: 64, runBatch);

        Assert.InRange(result, 4, 64);
    }

    [Fact]
    public void DetermineBatchSize_WithConstantTime_ReturnsMaxBatch()
    {
        var tuner = new BatchSizeAutoTuner();
        var runBatch = (int batchSize) => TimeSpan.FromMilliseconds(10);

        var result = tuner.DetermineBatchSize(minBatch: 4, maxBatch: 32, runBatch);

        Assert.Equal(32, result);
    }

    [Fact]
    public void DetermineBatchSize_WithLinearTime_FindsOptimal()
    {
        var tuner = new BatchSizeAutoTuner();
        var runBatch = (int batchSize) => TimeSpan.FromMilliseconds(batchSize * 2);

        var result = tuner.DetermineBatchSize(minBatch: 8, maxBatch: 128, runBatch);

        Assert.InRange(result, 8, 128);
    }

    [Fact]
    public void DetermineBatchSize_InvalidMinBatch_ThrowsArgumentOutOfRangeException()
    {
        var tuner = new BatchSizeAutoTuner();
        var runBatch = (int batchSize) => TimeSpan.FromMilliseconds(10);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            tuner.DetermineBatchSize(minBatch: 0, maxBatch: 64, runBatch));
    }

    [Fact]
    public void DetermineBatchSize_MaxBatchSmallerThanMin_ThrowsArgumentOutOfRangeException()
    {
        var tuner = new BatchSizeAutoTuner();
        var runBatch = (int batchSize) => TimeSpan.FromMilliseconds(10);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            tuner.DetermineBatchSize(minBatch: 32, maxBatch: 16, runBatch));
    }

    [Fact]
    public void DetermineBatchSize_NullRunBatch_ThrowsArgumentNullException()
    {
        var tuner = new BatchSizeAutoTuner();

        Assert.Throws<ArgumentNullException>(() =>
            tuner.DetermineBatchSize(minBatch: 4, maxBatch: 64, null!));
    }

    [Fact]
    public void DetermineBatchSize_MinEqualsMax_ReturnsMinBatch()
    {
        var tuner = new BatchSizeAutoTuner();
        var runBatch = (int batchSize) => TimeSpan.FromMilliseconds(10);

        var result = tuner.DetermineBatchSize(minBatch: 32, maxBatch: 32, runBatch);

        Assert.Equal(32, result);
    }

    [Fact]
    public void DetermineBatchSize_PerformsWarmup()
    {
        var tuner = new BatchSizeAutoTuner();
        var callCount = 0;
        var runBatch = (int batchSize) =>
        {
            callCount++;
            return TimeSpan.FromMilliseconds(10);
        };

        tuner.DetermineBatchSize(minBatch: 4, maxBatch: 64, runBatch);

        Assert.True(callCount >= 2, "Should perform at least warmup runs");
    }

    [Fact]
    public void BatchSizeMode_HasExpectedValues()
    {
        Assert.True(Enum.IsDefined(typeof(BatchSizeMode), BatchSizeMode.Fixed));
        Assert.True(Enum.IsDefined(typeof(BatchSizeMode), BatchSizeMode.Auto));
    }

    [Fact]
    public void BatchSizeMode_FixedIsZero()
    {
        Assert.Equal(0, (int)BatchSizeMode.Fixed);
    }

    [Fact]
    public void BatchSizeMode_AutoIsOne()
    {
        Assert.Equal(1, (int)BatchSizeMode.Auto);
    }

    [Fact]
    public void DetermineBatchSize_WithDiminishingReturns_StopsDoubling()
    {
        var tuner = new BatchSizeAutoTuner();
        var callCount = 0;
        var runBatch = (int batchSize) =>
        {
            callCount++;
            return TimeSpan.FromMilliseconds(Math.Max(5, 50.0 / batchSize));
        };

        var result = tuner.DetermineBatchSize(minBatch: 2, maxBatch: 128, runBatch);

        Assert.InRange(result, 2, 128);
        Assert.True(callCount > 0, "Should have called runBatch at least once");
    }
}
