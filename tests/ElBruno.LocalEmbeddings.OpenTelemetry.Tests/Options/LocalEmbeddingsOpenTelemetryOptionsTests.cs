using ElBruno.LocalEmbeddings.OpenTelemetry.Options;
using Xunit;

namespace ElBruno.LocalEmbeddings.OpenTelemetry.Tests.Options;

/// <summary>
/// Unit tests for LocalEmbeddingsOpenTelemetryOptions.
/// </summary>
public class LocalEmbeddingsOpenTelemetryOptionsTests
{
    [Fact]
    public void DefaultConstructor_InitializesWithDefaults()
    {
        var options = new LocalEmbeddingsOpenTelemetryOptions();

        Assert.True(options.EnableTracing);
        Assert.True(options.EnableMetrics);
        Assert.False(options.EnableBaggagePropagation);
        Assert.False(options.EnableBaggage);
        Assert.Equal(1.0, options.SamplingRate);
        Assert.True(options.RecordExceptionDetails);
        Assert.False(options.RecordBaggageInAttributes);
        Assert.Empty(options.BaggageItems);
        Assert.Equal(16, options.MaxBaggageItemsToRecord);
    }

    [Fact]
    public void Validate_Succeeds_WhenSamplingRateIsValid()
    {
        var options = new LocalEmbeddingsOpenTelemetryOptions { SamplingRate = 0.5 };
        
        options.Validate(); // Should not throw
    }

    [Fact]
    public void Validate_Throws_WhenSamplingRateIsBelowZero()
    {
        var options = new LocalEmbeddingsOpenTelemetryOptions { SamplingRate = -0.1 };
        
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Contains("SamplingRate must be between 0.0 and 1.0", ex.Message);
    }

    [Fact]
    public void Validate_Throws_WhenSamplingRateIsAboveOne()
    {
        var options = new LocalEmbeddingsOpenTelemetryOptions { SamplingRate = 1.1 };
        
        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Contains("SamplingRate must be between 0.0 and 1.0", ex.Message);
    }

    [Fact]
    public void Validate_Throws_WhenMaxBaggageItemsIsNegative()
    {
        var options = new LocalEmbeddingsOpenTelemetryOptions { MaxBaggageItemsToRecord = -1 };

        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Contains("MaxBaggageItemsToRecord must be greater than or equal to 0", ex.Message);
    }

    [Fact]
    public void EnableBaggage_Alias_MapsToEnableBaggagePropagation()
    {
        var options = new LocalEmbeddingsOpenTelemetryOptions();

        options.EnableBaggage = true;
        Assert.True(options.EnableBaggagePropagation);

        options.EnableBaggagePropagation = false;
        Assert.False(options.EnableBaggage);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Validate_Succeeds_WhenSamplingRateIsAtBoundary(double rate)
    {
        var options = new LocalEmbeddingsOpenTelemetryOptions { SamplingRate = rate };
        
        options.Validate(); // Should not throw
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void BaggageOptions_CanBeConfigured_WithoutAffectingValidation(
        bool enableBaggagePropagation,
        bool recordBaggageInAttributes)
    {
        var options = new LocalEmbeddingsOpenTelemetryOptions
        {
            SamplingRate = 0.75,
            EnableBaggagePropagation = enableBaggagePropagation,
            RecordBaggageInAttributes = recordBaggageInAttributes
        };

        options.Validate();

        Assert.Equal(enableBaggagePropagation, options.EnableBaggagePropagation);
        Assert.Equal(recordBaggageInAttributes, options.RecordBaggageInAttributes);
    }
}
