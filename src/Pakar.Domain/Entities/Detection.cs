namespace Pakar.Domain.Entities;

/// <summary>One detection result from a camera; kept as history for statistics and prediction.</summary>
public class Detection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public float ConfidenceScore { get; set; }
    public string BoundingBoxJson { get; set; } = string.Empty;
    public string SnapshotUrl { get; set; } = string.Empty;

    /// <summary>Occupancy at detection time: "occupied" or "available".</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Spot ID as reported by the AI pipeline, kept even when no database spot matches.</summary>
    public int SpotExternalId { get; set; }

    public Guid CameraId { get; set; }
    public Camera Camera { get; set; } = null!;

    /// <summary>Matching database spot, if any.</summary>
    public Guid? SpotId { get; set; }
    public ParkingSpot? Spot { get; set; }
}
