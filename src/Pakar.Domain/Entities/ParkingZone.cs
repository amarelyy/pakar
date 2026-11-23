using Pakar.Domain.Services;

namespace Pakar.Domain.Entities;

public class ParkingZone
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int TotalCapacity { get; set; }

    /// <summary>Area ID used by the AI pipeline configuration; assigned by the system, never reused.</summary>
    public int ExternalId { get; set; }

    /// <summary>
    /// Status an admin forced for this zone, for example while its camera is down (FR 17).
    /// While set, it replaces the status computed from the spots; null means no override.
    /// </summary>
    public ZoneOccupancyStatus? StatusOverride { get; set; }

    /// <summary>Why the override was set (for example "Camera 2 is offline"), shown to admins only.</summary>
    public string? StatusOverrideNote { get; set; }

    public DateTime? StatusOverrideSetAt { get; set; }

    /// <summary>Display name of the admin who set the override.</summary>
    public string? StatusOverrideSetBy { get; set; }

    /// <summary>Set when an admin removes the zone (FR 13); the row stays so detection history is kept.</summary>
    public DateTime? DeletedAt { get; set; }

    public ICollection<ParkingSpot> Spots { get; set; } = new List<ParkingSpot>();
    public ICollection<Camera> Cameras { get; set; } = new List<Camera>();
}
