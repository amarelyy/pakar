namespace Pakar.Vision;

/// <summary>Exact polygon and rectangle overlap, so no pixel mask is needed (works in the browser).</summary>
public static class PolygonOverlap
{
    /// <summary>Area of the polygon clipped to the rectangle (Sutherland-Hodgman against four edges).</summary>
    public static double IntersectionArea(double[][] polygon, double left, double top, double right, double bottom)
    {
        var points = polygon.Select(p => (X: p[0], Y: p[1])).ToList();
        points = Clip(points, p => p.X >= left, (a, b) => Cross(a, b, true, left));
        points = Clip(points, p => p.X <= right, (a, b) => Cross(a, b, true, right));
        points = Clip(points, p => p.Y >= top, (a, b) => Cross(a, b, false, top));
        points = Clip(points, p => p.Y <= bottom, (a, b) => Cross(a, b, false, bottom));
        return Area(points);
    }

    public static double Area(IReadOnlyList<(double X, double Y)> points)
    {
        if (points.Count < 3) return 0;
        double sum = 0;
        for (int i = 0; i < points.Count; i++)
        {
            var a = points[i]; var b = points[(i + 1) % points.Count];
            sum += a.X * b.Y - b.X * a.Y;
        }
        return Math.Abs(sum) / 2;
    }

    /// <summary>IoU between the polygon and the rectangle, in any shared coordinate system.</summary>
    public static double Iou(double[][] polygon, double left, double top, double right, double bottom)
    {
        double polygonArea = Area(polygon.Select(p => (p[0], p[1])).ToList());
        double boxArea = Math.Max(0, right - left) * Math.Max(0, bottom - top);
        double inter = IntersectionArea(polygon, left, top, right, bottom);
        double union = polygonArea + boxArea - inter;
        return union > 0 ? inter / union : 0;
    }

    private static List<(double X, double Y)> Clip(
        List<(double X, double Y)> input,
        Func<(double X, double Y), bool> inside,
        Func<(double X, double Y), (double X, double Y), (double X, double Y)> intersect)
    {
        var output = new List<(double X, double Y)>();
        for (int i = 0; i < input.Count; i++)
        {
            var current = input[i]; var previous = input[(i + input.Count - 1) % input.Count];
            bool currentInside = inside(current), previousInside = inside(previous);
            if (currentInside)
            {
                if (!previousInside) output.Add(intersect(previous, current));
                output.Add(current);
            }
            else if (previousInside)
            {
                output.Add(intersect(previous, current));
            }
        }
        return output;
    }

    private static (double X, double Y) Cross((double X, double Y) a, (double X, double Y) b, bool vertical, double edge)
    {
        if (vertical)
        {
            double t = (edge - a.X) / (b.X - a.X);
            return (edge, a.Y + t * (b.Y - a.Y));
        }
        double u = (edge - a.Y) / (b.Y - a.Y);
        return (a.X + u * (b.X - a.X), edge);
    }
}
