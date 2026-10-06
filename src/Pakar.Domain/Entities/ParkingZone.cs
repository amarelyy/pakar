namespace Pakar.Domain.Entities;

public class ParkingZone
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int TotalCapacity { get; set; }

    // Navigation Properties
    public ICollection<ParkingSpot> Spots { get; set; } = new List<ParkingSpot>();
    public ICollection<Camera> Cameras { get; set; } = new List<Camera>();
}