using Pakar.Application.DTOs;
using Pakar.Shared.Geometry;

namespace Pakar.Application.Validation;

public static class ZoneMapAreaRules
{
    public const int MaxPoints = 50;
    public const int MaxLabelLength = 24;
    private const double South = -7.7705;
    private const double West = 110.3694;
    private const double North = -7.7641;
    private const double East = 110.3784;

    public static double[][] CampusBounds => [[South, West], [North, East]];

    public static string? Validate(ZoneMapAreaDto area)
    {
        if (area.Points is null)
            return "Supply the boundary points.";
        if (area.Points.Length == 0)
            return null;
        if (area.Points.Length is < 3 or > MaxPoints)
            return $"Draw a boundary with 3 to {MaxPoints} points.";
        if (area.Label?.Trim().Length > MaxLabelLength)
            return $"Use at most {MaxLabelLength} characters for the map label.";

        var points = area.Points;
        if (points.Any(point => point is null ||
            !(point.Latitude >= South && point.Latitude <= North &&
              point.Longitude >= West && point.Longitude <= East)))
            return "Place every point inside the campus map.";
        if (points.Distinct().Count() != points.Length)
            return "Use a different location for each boundary point.";

        // Scale to the campus map so geometry tolerances stay independent of latitude and longitude magnitudes.
        var coordinates = points.Select(point => new[]
        {
            (point.Longitude - West) / (East - West),
            (point.Latitude - South) / (North - South)
        }).ToArray();
        if (PolygonRules.HasIntersectingEdges(coordinates))
            return "Draw the boundary in order around the parking area so the edges do not cross.";

        return PolygonRules.Area(coordinates) < 1e-9 ? "Draw a boundary that encloses an area." : null;
    }
}
