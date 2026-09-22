namespace Pakar.Domain.Entities;

public class Detection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public float ConfidenceScore { get; set; }
    public string BoundingBoxJson { get; set; } = string.Empty; // Raw output from YOLO
    public string SnapshotUrl { get; set; } = string.Empty; // URL ke Azure Blob Storage

    // Foreign Keys
    public Guid CameraId { get; set; }
    public Camera Camera { get; set; } = null!;
}