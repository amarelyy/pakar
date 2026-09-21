namespace Pakar.Domain.Entities;

public class ParkingZone
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty; // e.g., "Lapangan Parkir Utama"
    public int TotalCapacity { get; set; }
    
    // FR 17: Admin override untuk kapasitas manual jika sensor error
    public int? ManualOverrideCount { get; set; } 

    // Navigation Properties
    public ICollection<ParkingSpot> Spots { get; set; } = new List<ParkingSpot>();
    public ICollection<Camera> Cameras { get; set; } = new List<Camera>();
    public ICollection<SavedZone> SavedZones { get; set; } = new List<SavedZone>();
}