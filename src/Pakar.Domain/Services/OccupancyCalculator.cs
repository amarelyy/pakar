namespace Pakar.Domain.Services;

/// <summary>Zone availability shown to users (FR 1, FR 11). API values: available, almost_full, full.</summary>
public enum ZoneOccupancyStatus
{
    Available,
    AlmostFull,
    Full
}

/// <summary>Single source of the zone status rule, used by the REST API and the realtime hub.</summary>
public static class OccupancyCalculator
{
    /// <summary>A zone at or above this share of occupied spots is "almost full".</summary>
    public const double AlmostFullRatio = 0.9;

    /// <summary>Capacity never drops below the number of mapped spots, so extra spots still count.</summary>
    public static int EffectiveCapacity(int configuredCapacity, int mappedSpots) =>
        Math.Max(Math.Max(configuredCapacity, 0), Math.Max(mappedSpots, 0));

    public static int Available(int capacity, int occupied) => Math.Max(capacity - occupied, 0);

    public static ZoneOccupancyStatus GetStatus(int capacity, int occupied)
    {
        // A zone without capacity has nothing to fill yet; it is shown as available.
        if (capacity <= 0)
            return ZoneOccupancyStatus.Available;
        if (occupied >= capacity)
            return ZoneOccupancyStatus.Full;
        return (double)occupied / capacity >= AlmostFullRatio
            ? ZoneOccupancyStatus.AlmostFull
            : ZoneOccupancyStatus.Available;
    }

    public static string ToApiValue(this ZoneOccupancyStatus status) => status switch
    {
        ZoneOccupancyStatus.Available => "available",
        ZoneOccupancyStatus.AlmostFull => "almost_full",
        ZoneOccupancyStatus.Full => "full",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    /// <summary>Parses an API value ("almost_full") or enum name ("AlmostFull"), ignoring case.</summary>
    public static bool TryParseApiValue(string? value, out ZoneOccupancyStatus status)
    {
        var normalized = value?.Trim().Replace("_", string.Empty);
        return Enum.TryParse(normalized, ignoreCase: true, out status) && Enum.IsDefined(status);
    }
}
