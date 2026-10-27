namespace Pakar.Domain.Entities;

public class ParkingZone
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int TotalCapacity { get; set; }

    /// <summary>Area ID used by the AI pipeline configuration; assigned by the system, never reused.</summary>
    public int ExternalId { get; set; }

    /// <summary>Set when an admin removes the zone (FR 13); the row stays so detection history is kept.</summary>
    public DateTime? DeletedAt { get; set; }

    public ICollection<ParkingSpot> Spots { get; set; } = new List<ParkingSpot>();
    public ICollection<Camera> Cameras { get; set; } = new List<Camera>();
}
