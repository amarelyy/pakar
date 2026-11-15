using Pakar.Shared.Dtos;
using Pakar.Shared.Geometry;

namespace Pakar.Application.Validation;

public static class ProcessedFrameRules
{
    public static string? Validate(ProcessedFrameDto? frame, DateTimeOffset now)
    {
        if (frame?.Observation is not {} o || o.CameraId<=0 || o.AreaId<=0 ||
            frame.Jpeg is null || frame.Jpeg.Length is < 4 or > 400000 || frame.Jpeg[0]!=255 || frame.Jpeg[1]!=216 ||
            string.IsNullOrWhiteSpace(frame.VideoFile) || frame.VideoFile.Length>255 || frame.VideoFile.IndexOfAny(['/', '\\', ':'])>=0 || frame.VideoFile.Contains("..") ||
            !double.IsFinite(o.MediaSeconds) || o.MediaSeconds<0 || o.ObservedAt>now.AddSeconds(5) || o.ObservedAt<now.AddSeconds(-15)) return "Invalid frame or observation timing.";
        if(frame.Jpeg[^2]!=255 || frame.Jpeg[^1]!=217 || ImageDimensions.Read(frame.Jpeg) is not {} size ||
            size.Width>1920 || size.Height>1080) return "Invalid or oversized JPEG dimensions.";
        if (o.Slots is null || o.Slots.Count is < 1 or > 500 || frame.Polygons is null || frame.Polygons.Count!=o.Slots.Count ||
            frame.Boxes is null || frame.Boxes.Count>1000) return "Invalid collection sizes.";
        if (o.Slots.Any(s=>s is null || s.SpotId<=0 || string.IsNullOrWhiteSpace(s.SpotNumber) || s.SpotNumber.Length>200 ||
            s.Status is not (null or "available" or "occupied") || !double.IsFinite(s.Confidence) || s.Confidence is <0 or >1 || s.Reason?.Length>128) ||
            o.Slots.Select(s=>s.SpotId).Distinct().Count()!=o.Slots.Count) return "Invalid slot observations.";
        if (frame.Polygons.Any(p=>p is null || p.Polygon is null || p.Polygon.Any(point=>point is null) || PolygonRules.Validate(p.Polygon) is not null) ||
            !o.Slots.Select(s=>s.SpotId).Order().SequenceEqual(frame.Polygons.Select(p=>p.SpotId).Order())) return "Invalid slot polygons.";
        if (frame.Boxes.Any(b=>b is null || b.Label is not ("empty" or "occupied") ||
            !float.IsFinite(b.Confidence) || b.Confidence is <0 or >1 ||
            new[]{b.X,b.Y,b.Width,b.Height}.Any(v=>!float.IsFinite(v)) || b.X<0 || b.Y<0 || b.Width<=0 || b.Height<=0 || b.X+b.Width>1.00001 || b.Y+b.Height>1.00001)) return "Invalid detection boxes.";
        return null;
    }
}
