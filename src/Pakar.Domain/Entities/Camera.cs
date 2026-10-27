namespace Pakar.Domain.Entities;

public class Camera
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string StreamUrl { get; set; } = string.Empty;
    public string ConnectionType { get; set; } = "IP";
    public string ApiKeyHash { get; set; } = string.Empty;

    /// <summary>Camera ID used by the AI pipeline configuration; assigned by the system, never reused.</summary>
    public int ExternalId { get; set; }

    /// <summary>Last time the camera sent a detection or heartbeat, in UTC (FR 12).</summary>
    public DateTime? LastActiveTime { get; set; }

    /// <summary>Set when an admin removes the camera (FR 14); the row stays so detection history is kept.</summary>
    public DateTime? DeletedAt { get; set; }

    public Guid ZoneId { get; set; }
    public ParkingZone Zone { get; set; } = null!;
}
