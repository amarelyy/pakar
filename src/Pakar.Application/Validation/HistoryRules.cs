using Pakar.Application.DTOs;

namespace Pakar.Application.Validation;

public enum HistoryBucket
{
    Hour,
    Day
}

/// <summary>A validated time window for history queries, in UTC.</summary>
public sealed record HistoryWindow(DateTime From, DateTime To, HistoryBucket Bucket);

/// <summary>Defaults and limits for history queries, so one request can never scan the whole table.</summary>
public static class HistoryRules
{
    public static readonly TimeSpan MaxHourlyRange = TimeSpan.FromDays(7);
    public static readonly TimeSpan MaxDailyRange = TimeSpan.FromDays(92);
    public static readonly TimeSpan DefaultHourlyRange = TimeSpan.FromHours(24);
    public static readonly TimeSpan DefaultDailyRange = TimeSpan.FromDays(30);

    /// <summary>Largest window for the admin list of raw records.</summary>
    public static readonly TimeSpan MaxRecordRange = TimeSpan.FromDays(31);

    public static string ToApiValue(this HistoryBucket bucket) => bucket == HistoryBucket.Day ? "day" : "hour";

    /// <summary>Window for the per-zone series. Missing "to" is now; missing "from" is the default range before "to".</summary>
    public static (HistoryWindow? Window, Dictionary<string, string[]> Errors) ResolveSeries(ZoneHistoryQuery query, DateTime utcNow)
    {
        var errors = new Dictionary<string, string[]>();
        var bucket = HistoryBucket.Hour;
        if (!string.IsNullOrWhiteSpace(query.Bucket))
        {
            switch (query.Bucket.Trim().ToLowerInvariant())
            {
                case "hour": bucket = HistoryBucket.Hour; break;
                case "day": bucket = HistoryBucket.Day; break;
                default: errors[nameof(query.Bucket)] = ["Bucket must be hour or day."]; break;
            }
        }

        var to = query.To?.UtcDateTime ?? utcNow;
        var from = query.From?.UtcDateTime ?? to - (bucket == HistoryBucket.Day ? DefaultDailyRange : DefaultHourlyRange);
        var maxRange = bucket == HistoryBucket.Day ? MaxDailyRange : MaxHourlyRange;

        if (from >= to)
            errors[nameof(query.From)] = ["From must be earlier than To."];
        else if (to - from > maxRange)
            errors[nameof(query.To)] = [$"The range for hourly buckets is at most {MaxHourlyRange.TotalDays:0} days and for daily buckets {MaxDailyRange.TotalDays:0} days."];

        return errors.Count > 0 ? (null, errors) : (new HistoryWindow(from, to, bucket), errors);
    }

    /// <summary>Window for the admin list of raw records; the default is the last 24 hours.</summary>
    public static (HistoryWindow? Window, Dictionary<string, string[]> Errors) ResolveRecords(DateTimeOffset? from, DateTimeOffset? to, DateTime utcNow)
    {
        var errors = new Dictionary<string, string[]>();
        var end = to?.UtcDateTime ?? utcNow;
        var start = from?.UtcDateTime ?? end - DefaultHourlyRange;

        if (start >= end)
            errors["From"] = ["From must be earlier than To."];
        else if (end - start > MaxRecordRange)
            errors["To"] = [$"The range is at most {MaxRecordRange.TotalDays:0} days."];

        return errors.Count > 0 ? (null, errors) : (new HistoryWindow(start, end, HistoryBucket.Hour), errors);
    }

    public static bool IsValidStatus(string? status) => status is "occupied" or "available";
}
