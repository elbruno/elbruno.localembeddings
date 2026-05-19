using ElBruno.LocalEmbeddings.OpenTelemetry.Options;
using Xunit;

namespace ElBruno.LocalEmbeddings.OpenTelemetry.Tests.Options;

/// <summary>
/// Unit tests for sampling configuration and behavior.
/// </summary>
public class SamplingTests
{
    [Fact]
    public void OTEL_Sampling_Applied_ShouldSample_AlwaysTrue_When_SamplingRateIs100Percent()
    {
        var options = new LocalEmbeddingsOpenTelemetryOptions { SamplingRate = 1.0 };
        
        for (int i = 0; i < 100; i++)
        {
            Assert.True(options.ShouldSample());
        }
    }

    [Fact]
    public void OTEL_Sampling_Applied_ShouldSample_AlwaysFalse_When_SamplingRateIs0Percent()
    {
        var options = new LocalEmbeddingsOpenTelemetryOptions { SamplingRate = 0.0 };
        
        for (int i = 0; i < 100; i++)
        {
            Assert.False(options.ShouldSample());
        }
    }

    [Fact]
    public void OTEL_Sampling_Applied_ShouldSample_HonorsSamplingRate_At10Percent()
    {
        var options = new LocalEmbeddingsOpenTelemetryOptions { SamplingRate = 0.1 };
        
        int sampledCount = 0;
        int attempts = 10000;
        
        for (int i = 0; i < attempts; i++)
        {
            if (options.ShouldSample())
            {
                sampledCount++;
            }
        }
        
        double actualRate = (double)sampledCount / attempts;
        
        // Allow 20% deviation from target (0.08-0.12 for 10% target)
        Assert.True(actualRate >= 0.08 && actualRate <= 0.12,
            $"Sampling rate {actualRate} outside acceptable range [0.08, 0.12]");
    }

    [Fact]
    public void OTEL_Sampling_Applied_ShouldSample_HonorsSamplingRate_At50Percent()
    {
        var options = new LocalEmbeddingsOpenTelemetryOptions { SamplingRate = 0.5 };
        
        int sampledCount = 0;
        int attempts = 10000;
        
        for (int i = 0; i < attempts; i++)
        {
            if (options.ShouldSample())
            {
                sampledCount++;
            }
        }
        
        double actualRate = (double)sampledCount / attempts;
        
        // Allow 20% deviation from target (0.4-0.6 for 50% target)
        Assert.True(actualRate >= 0.4 && actualRate <= 0.6,
            $"Sampling rate {actualRate} outside acceptable range [0.4, 0.6]");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.25)]
    [InlineData(0.5)]
    [InlineData(0.75)]
    [InlineData(1.0)]
    public void OTEL_Sampling_Applied_ValidSamplingRates_ProduceBooleanResult(double rate)
    {
        var options = new LocalEmbeddingsOpenTelemetryOptions { SamplingRate = rate };
        
        bool result = options.ShouldSample();
        
        Assert.IsType<bool>(result);
    }

    [Fact]
    public void OTEL_Sampling_Applied_DefaultSamplingRate_IsFullCoverage()
    {
        var options = new LocalEmbeddingsOpenTelemetryOptions();
        
        // Default should be 1.0 (sample all)
        Assert.Equal(1.0, options.SamplingRate);
        
        // Verify it returns true
        Assert.True(options.ShouldSample());
    }
}
