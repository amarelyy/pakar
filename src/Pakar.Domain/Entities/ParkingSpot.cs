namespace Pakar.Domain.Entities;

public class ParkingSpot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty; 
    
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    
    public bool IsOccupied { get; set; } = false;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>Integer ID dari konfigurasi AI pipeline / ROI config (spot_id int ↔ Guid)</summary>
    public int ExternalId { get; set; }

    public Guid ZoneId { get; set; }
    public ParkingZone Zone { get; set; } = null!;

    public ICollection<Report> Reports { get; set; } = new List<Report>();
}