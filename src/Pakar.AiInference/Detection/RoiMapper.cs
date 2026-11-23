using OpenCvSharp;
using Pakar.Shared.Dtos;
using Pakar.Shared.Geometry;
using Pakar.Vision;

namespace Pakar.AiInference.Detection;

/// <summary>Slot-class detector adapter; raster IoU follows the Python reference.</summary>
public sealed class RoiMapper : IDisposable
{
    private readonly List<WorkerSlotDto> slots;
    private readonly double minConfidence, minIou, conflictMargin;
    private readonly Dictionary<int, (Mat Mask, double Area, double[][] Polygon)> prepared = [];
    private (int, int) size;

    public RoiMapper(List<WorkerSlotDto> slots, double minConfidence = .10, double minIou = .10, double conflictMargin = .10)
    {
        if (slots.Count == 0 || slots.Any(s => s.SpotId <= 0 || string.IsNullOrWhiteSpace(s.SpotNumber) || s.SpotNumber != s.SpotNumber.Trim()) ||
            slots.Select(s => s.SpotId).Distinct().Count() != slots.Count ||
            slots.Select(s => s.SpotNumber).Distinct().Count() != slots.Count)
            throw new ArgumentException("Slot IDs and names must be positive/unique and names nonblank without padding.");
        foreach (var slot in slots)
            if (PolygonRules.Validate(slot.Polygon) is { } error) throw new ArgumentException(error);
        if (new[] { minConfidence, minIou, conflictMargin }.Any(v => !double.IsFinite(v) || v < 0 || v > 1))
            throw new ArgumentException("Thresholds must be in 0..1.");
        this.slots = slots; this.minConfidence = minConfidence; this.minIou = minIou; this.conflictMargin = conflictMargin;
    }

    public Dictionary<int, Observation> Map(IReadOnlyList<BoundingBoxDto> detections, int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentException("Invalid frame dimensions.");
        if (size != (width, height))
        {
            Dispose();
            foreach (var slot in slots)
            {
                var points = slot.Polygon.Select(p => new Point((int)Math.Round(p[0] * width), (int)Math.Round(p[1] * height))).ToArray();
                var mask = new Mat(height, width, MatType.CV_8UC1, Scalar.All(0));
                Cv2.FillPoly(mask, new[] { points }, Scalar.All(1));
                prepared[slot.SpotId] = (mask, Cv2.CountNonZero(mask), points.Select(p => new[] { (double)p.X, p.Y }).ToArray());
            }
            size = (width, height);
        }
        var hits = slots.ToDictionary(s => s.SpotId, _ => new Dictionary<string, double>());
        foreach (var d in detections)
        {
            if (d.Label is not ("empty" or "occupied") || !float.IsFinite(d.Confidence) || d.Confidence < minConfidence ||
                new[] { d.X, d.Y, d.Width, d.Height }.Any(v => !float.IsFinite(v))) continue;
            int x1 = (int)Math.Clamp(d.X * width, 0, width), y1 = (int)Math.Clamp(d.Y * height, 0, height);
            int x2 = (int)Math.Clamp((d.X + d.Width) * width, 0, width), y2 = (int)Math.Clamp((d.Y + d.Height) * height, 0, height);
            if (x2 <= x1 || y2 <= y1) continue;
            int? best = null; double bestIou = -1;
            foreach (var slot in slots)
            {
                var (mask, area, polygon) = prepared[slot.SpotId];
                if (!PolygonRules.Contains(polygon, (x1 + x2) / 2d, (y1 + y2) / 2d)) continue;
                using var intersection = new Mat(mask, new Rect(x1, y1, x2 - x1, y2 - y1));
                double inter = Cv2.CountNonZero(intersection);
                double iou = inter / (area + (x2 - x1) * (y2 - y1) - inter);
                if (iou >= minIou && iou > bestIou) { best = slot.SpotId; bestIou = iou; }
            }
            if (best is { } id) hits[id][d.Label] = Math.Max(hits[id].GetValueOrDefault(d.Label), d.Confidence);
        }
        return hits.ToDictionary(kv => kv.Key, kv =>
        {
            var labels = kv.Value;
            if (labels.Count == 0) return new Observation(null, 0, "no_matching_detection");
            if (labels.Count == 2 && Math.Abs(labels["empty"] - labels["occupied"]) < conflictMargin)
                return new Observation(null, 0, "conflicting_labels");
            var best = labels.MaxBy(p => p.Value);
            return new Observation(best.Key == "empty" ? "available" : "occupied", best.Value, null);
        });
    }

    public void Dispose()
    {
        foreach (var item in prepared.Values) item.Mask.Dispose();
        prepared.Clear(); size = default;
    }
}
