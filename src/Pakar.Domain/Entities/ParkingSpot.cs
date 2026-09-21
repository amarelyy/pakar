namespace Pakar.Domain.Entities;

public class ParkingSpot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty; // e.g., "A-01"
    
    // Koordinat untuk Leaflet Map
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    
    public bool IsOccupied { get; set; } = false;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    // Foreign Key & Navigation
    public Guid ZoneId { get; set; }
    public ParkingZone Zone { get; set; } = null!;
    
    // Relasi ke Report (FR 5, 6)
    public ICollection<Report> Reports { get; set; } = new List<Report>();
}