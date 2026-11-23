using Pakar.Application.DTOs;
using Pakar.Application.Validation;

namespace Pakar.Api.Tests;

public class HistoryRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TheDefaultSeriesIsTheLastDayInHourlyBuckets()
    {
        var (window, errors) = HistoryRules.ResolveSeries(new ZoneHistoryQuery(null, null, null), Now);
        Assert.Empty(errors);
        Assert.Equal(HistoryBucket.Hour, window!.Bucket);
        Assert.Equal(Now, window.To);
        Assert.Equal(Now.AddHours(-24), window.From);
    }

    [Fact]
    public void DailyBucketsDefaultToThirtyDays()
    {
        var (window, _) = HistoryRules.ResolveSeries(new ZoneHistoryQuery(null, null, "DAY"), Now);
        Assert.Equal(HistoryBucket.Day, window!.Bucket);
        Assert.Equal(Now.AddDays(-30), window.From);
    }

    [Fact]
    public void ExplicitTimesAreConvertedToUtc()
    {
        var from = new DateTimeOffset(2026, 11, 20, 7, 0, 0, TimeSpan.FromHours(7));
        var to = new DateTimeOffset(2026, 11, 21, 7, 0, 0, TimeSpan.FromHours(7));
        var (window, _) = HistoryRules.ResolveSeries(new ZoneHistoryQuery(from, to, "hour"), Now);
        Assert.Equal(new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc), window!.From);
        Assert.Equal(new DateTime(2026, 11, 21, 0, 0, 0, DateTimeKind.Utc), window.To);
    }

    [Theory]
    [InlineData("week")]
    [InlineData("minute")]
    public void UnknownBucketsAreRejected(string bucket)
    {
        var (window, errors) = HistoryRules.ResolveSeries(new ZoneHistoryQuery(null, null, bucket), Now);
        Assert.Null(window);
        Assert.Contains(nameof(ZoneHistoryQuery.Bucket), errors.Keys);
    }

    [Fact]
    public void RangesAreLimitedPerBucket()
    {
        var hourly = new ZoneHistoryQuery(Now.AddDays(-8), Now, "hour");
        Assert.Null(HistoryRules.ResolveSeries(hourly, Now).Window);
        Assert.NotNull(HistoryRules.ResolveSeries(hourly with { From = Now.AddDays(-7) }, Now).Window);

        var daily = new ZoneHistoryQuery(Now.AddDays(-93), Now, "day");
        Assert.Null(HistoryRules.ResolveSeries(daily, Now).Window);
        Assert.NotNull(HistoryRules.ResolveSeries(daily with { From = Now.AddDays(-92) }, Now).Window);
    }

    [Fact]
    public void ABackwardsRangeIsRejected()
    {
        var (window, errors) = HistoryRules.ResolveSeries(new ZoneHistoryQuery(Now, Now.AddHours(-1), null), Now);
        Assert.Null(window);
        Assert.Contains(nameof(ZoneHistoryQuery.From), errors.Keys);
    }

    [Fact]
    public void RecordsDefaultToTheLastDayAndAreLimitedToAMonth()
    {
        var (window, _) = HistoryRules.ResolveRecords(null, null, Now);
        Assert.Equal(Now.AddHours(-24), window!.From);
        Assert.Null(HistoryRules.ResolveRecords(Now.AddDays(-32), Now, Now).Window);
        Assert.NotNull(HistoryRules.ResolveRecords(Now.AddDays(-31), Now, Now).Window);
    }

    [Theory]
    [InlineData("occupied", true)]
    [InlineData("available", true)]
    [InlineData("full", false)]
    [InlineData(null, false)]
    public void OnlyRealSpotStatusesAreAccepted(string? status, bool valid) =>
        Assert.Equal(valid, HistoryRules.IsValidStatus(status));
}
