namespace Pakar.Shared.Geometry;

public static class PolygonRules
{
    public static string? Validate(double[][]? points)
    {
        if (points is null || points.Length is < 3 or > 50) return "Polygon needs 3 to 50 points.";
        if (points.Any(p => p is null || p.Length != 2 || p.Any(v => !double.IsFinite(v) || v < 0 || v > 1)))
            return "Coordinates must be finite x/y pairs in 0..1.";
        if (points.Select(p => (p[0], p[1])).Distinct().Count() != points.Length)
            return "Polygon vertices must be distinct; do not repeat the first point.";
        if (Area(points) < .0005) return "Polygon area must be at least 0.0005.";
        for (int i = 0; i < points.Length; i++)
            for (int j = i + 1; j < points.Length; j++)
            {
                if (j == i + 1 || (i == 0 && j == points.Length - 1)) continue;
                if (Intersects(points[i], points[(i + 1) % points.Length], points[j], points[(j + 1) % points.Length]))
                    return "Polygon edges must not intersect.";
            }
        return null;
    }

    public static double Area(double[][] p) => Math.Abs(Enumerable.Range(0, p.Length)
        .Sum(i => p[i][0] * p[(i + 1) % p.Length][1] - p[(i + 1) % p.Length][0] * p[i][1])) / 2;

    private static double Cross(double[] a, double[] b, double[] p) =>
        (b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0]);

    private static bool OnSegment(double[] a, double[] b, double[] p) =>
        Math.Abs(Cross(a, b, p)) < 1e-10 && p[0] >= Math.Min(a[0], b[0]) - 1e-10 &&
        p[0] <= Math.Max(a[0], b[0]) + 1e-10 && p[1] >= Math.Min(a[1], b[1]) - 1e-10 &&
        p[1] <= Math.Max(a[1], b[1]) + 1e-10;

    private static bool Intersects(double[] a, double[] b, double[] c, double[] d) =>
        Cross(a, b, c) * Cross(a, b, d) < 0 && Cross(c, d, a) * Cross(c, d, b) < 0 ||
        OnSegment(a, b, c) || OnSegment(a, b, d) || OnSegment(c, d, a) || OnSegment(c, d, b);

    /// <summary>Ray casting with boundary included. Works in normalized or pixel coordinates.</summary>
    public static bool Contains(double[][] polygon, double x, double y)
    {
        bool inside = false;
        double[] point = [x, y];
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            var a = polygon[j]; var b = polygon[i];
            if (OnSegment(a, b, point)) return true;
            if ((a[1] > y) != (b[1] > y) && x < (b[0] - a[0]) * (y - a[1]) / (b[1] - a[1]) + a[0])
                inside = !inside;
        }
        return inside;
    }
}
