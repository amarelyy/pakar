using Pakar.Shared.Dtos;
using Pakar.Shared.Geometry;

namespace Pakar.Vision;

/// <summary>
/// Managed slot mapper: a detection belongs to a slot when its centre is inside the slot polygon and the
/// overlap is large enough. Same rules and thresholds as the OpenCV based RoiMapper in Pakar.AiInference,
/// but polygon and box are intersected exactly instead of through a raster mask. Boxes and polygons are
/// normalized (0..1), so no frame size is needed.
/// </summary>
public sealed class SlotMapper
{
    private readonly List<WorkerSlotDto> slots;
    private readonly double minConfidence, minIou, conflictMargin;

    public SlotMapper(List<WorkerSlotDto> slots, double minConfidence = .10, double minIou = .10, double conflictMargin = .10)
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

    public Dictionary<int, Observation> Map(IReadOnlyList<BoundingBoxDto> detections)
    {
        var hits = slots.ToDictionary(s => s.SpotId, _ => new Dictionary<string, double>());
        foreach (var d in detections)
        {
            if (d.Label is not ("empty" or "occupied") || !float.IsFinite(d.Confidence) || d.Confidence < minConfidence ||
                new[] { d.X, d.Y, d.Width, d.Height }.Any(v => !float.IsFinite(v))) continue;
            double x1 = Math.Clamp(d.X, 0, 1), y1 = Math.Clamp(d.Y, 0, 1);
            double x2 = Math.Clamp(d.X + d.Width, 0, 1), y2 = Math.Clamp(d.Y + d.Height, 0, 1);
            if (x2 <= x1 || y2 <= y1) continue;
            int? best = null; double bestIou = -1;
            foreach (var slot in slots)
            {
                if (!PolygonRules.Contains(slot.Polygon, (x1 + x2) / 2, (y1 + y2) / 2)) continue;
                double iou = PolygonOverlap.Iou(slot.Polygon, x1, y1, x2, y2);
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
}
