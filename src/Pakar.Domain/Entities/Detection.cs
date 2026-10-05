namespace Pakar.Domain.Entities;

public class Detection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public float ConfidenceScore { get; set; }
    public string BoundingBoxJson { get; set; } = string.Empty;
    public string SnapshotUrl { get; set; } = string.Empty;

    /// <summary>Status okupansi: "occupied" atau "available" pada saat deteksi ini</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>External Spot ID (int dari AI pipeline / ROI config) untuk logging cepat</summary>
    public int SpotExternalId { get; set; }

    public Guid CameraId { get; set; }
    public Camera Camera { get; set; } = null!;

    /// <summary>FK opsional ke ParkingSpot (nullable karena detection bisa tanpa mapping ke DB spot)</summary>
    public Guid? SpotId { get; set; }
    public ParkingSpot? Spot { get; set; }
}