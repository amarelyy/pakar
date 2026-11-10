namespace Pakar.Domain.Services;

public readonly record struct GeoPoint(double Latitude, double Longitude)
{
    public static bool IsValid(double latitude, double longitude) =>
        double.IsFinite(latitude) && double.IsFinite(longitude)
        && latitude is >= -90 and <= 90
        && longitude is >= -180 and <= 180;
}

/// <summary>Great-circle distance for ranking nearby zones (FR 2, FR 4).</summary>
public static class GeoDistance
{
    private const double EarthRadiusMeters = 6_371_000;

    public static double HaversineMeters(GeoPoint from, GeoPoint to)
    {
        var dLat = ToRadians(to.Latitude - from.Latitude);
        var dLon = ToRadians(to.Longitude - from.Longitude);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(ToRadians(from.Latitude)) * Math.Cos(ToRadians(to.Latitude))
                * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
