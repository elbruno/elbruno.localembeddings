using System.Diagnostics;
using ElBruno.LocalEmbeddings.OpenTelemetry.Internal;
using ElBruno.LocalEmbeddings.OpenTelemetry.Options;
using Xunit;

namespace ElBruno.LocalEmbeddings.OpenTelemetry.Tests.Internal;

public class BaggageExtensionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryReadFromHeader_ReturnsFalse_WhenHeaderEmpty(string? header)
    {
        var provider = new TestBaggageProvider();

        bool parsed = BaggageExtensions.TryReadFromHeader(header, provider);

        Assert.False(parsed);
        Assert.Empty(provider.Values);
    }

    [Fact]
    public void TryReadFromHeader_ReturnsTrue_WhenHeaderContainsValidBaggage()
    {
        var provider = new TestBaggageProvider();

        bool parsed = BaggageExtensions.TryReadFromHeader(
            "trace.user_id=user-123,trace.request_id=req-xyz;metadata=1",
            provider);

        Assert.True(parsed);
        Assert.Equal("user-123", provider.Values["trace.user_id"]);
        Assert.Equal("req-xyz", provider.Values["trace.request_id"]);
    }

    [Fact]
    public void TryReadFromHeader_IgnoresInvalidPairs_WhenSomeEntriesAreMalformed()
    {
        var provider = new TestBaggageProvider();

        bool parsed = BaggageExtensions.TryReadFromHeader(
            "missing-equals,=missingkey,trace.user_id=user-123",
            provider);

        Assert.True(parsed);
        Assert.Single(provider.Values);
        Assert.Equal("user-123", provider.Values["trace.user_id"]);
    }

    [Fact]
    public void AttachBaggageToActivity_Skips_WhenBaggageRecordingDisabled()
    {
        using var activity = new Activity("test").Start();
        var provider = new TestBaggageProvider();
        provider.Values["trace.user_id"] = "user-123";
        var options = new LocalEmbeddingsOpenTelemetryOptions
        {
            EnableBaggagePropagation = true,
            RecordBaggageInAttributes = false,
        };

        BaggageExtensions.AttachBaggageToActivity(activity, options, provider);

        Assert.Null(activity.GetTagItem("baggage.trace.user_id"));
    }

    [Fact]
    public void AttachBaggageToActivity_AttachesProviderAndCustomBaggage()
    {
        using var activity = new Activity("test").Start();
        var provider = new TestBaggageProvider();
        provider.Values["trace.user_id"] = "user-123";

        var options = new LocalEmbeddingsOpenTelemetryOptions
        {
            EnableBaggagePropagation = true,
            RecordBaggageInAttributes = true,
            MaxBaggageItemsToRecord = 4,
            BaggageItems = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["environment"] = "test",
            },
        };

        BaggageExtensions.AttachBaggageToActivity(activity, options, provider);

        Assert.Equal("user-123", activity.GetTagItem("baggage.trace.user_id"));
        Assert.Equal("test", activity.GetTagItem("baggage.environment"));
    }

    [Fact]
    public void AttachBaggageToActivity_HonorsMaxBaggageLimit()
    {
        using var activity = new Activity("test").Start();
        var provider = new TestBaggageProvider();
        provider.Values["trace.user_id"] = "user-123";
        provider.Values["trace.request_id"] = "req-xyz";

        var options = new LocalEmbeddingsOpenTelemetryOptions
        {
            EnableBaggagePropagation = true,
            RecordBaggageInAttributes = true,
            MaxBaggageItemsToRecord = 1,
            BaggageItems = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["environment"] = "test",
            },
        };

        BaggageExtensions.AttachBaggageToActivity(activity, options, provider);

        Assert.Equal("user-123", activity.GetTagItem("baggage.trace.user_id"));
        Assert.Null(activity.GetTagItem("baggage.trace.request_id"));
        Assert.Null(activity.GetTagItem("baggage.environment"));
    }

    private sealed class TestBaggageProvider : IActivityBaggageProvider
    {
        public Dictionary<string, string?> Values { get; } = new(StringComparer.Ordinal);

        public IEnumerable<KeyValuePair<string, string?>> GetBaggage() => Values;

        public void SetBaggage(string key, string? value) => Values[key] = value;

        public bool TryReadFromHeader(string? baggageHeader) => BaggageExtensions.TryReadFromHeader(baggageHeader, this);
    }
}
