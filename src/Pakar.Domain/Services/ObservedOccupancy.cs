namespace Pakar.Domain.Services;

/// <summary>Public availability is based on readings, never on unobserved capacity.</summary>
public static class ObservedOccupancy
{
    public const string Unknown = "unknown";
    public static readonly TimeSpan Freshness = TimeSpan.FromMinutes(2);

    public static string StatusAt(string? status, DateTimeOffset? observedAt, DateTimeOffset now) =>
        (status is "available" or "occupied") && observedAt is { } at && now - at < Freshness
            ? status
            : Unknown;

    public static (string Status, int Available, int Unknown) Resolve(
        int capacity, int occupied, int available, ZoneOccupancyStatus? manual)
    {
        var unknown = Math.Max(0, capacity - occupied - available);
        var status = manual?.ToApiValue() ?? (unknown > 0
            ? Unknown
            : OccupancyCalculator.GetStatus(capacity, occupied).ToApiValue());

        return (status, manual == ZoneOccupancyStatus.Full ? 0 : available, unknown);
    }
}
