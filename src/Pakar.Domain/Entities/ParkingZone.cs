namespace Pakar.Domain.Entities;

public class ParkingZone
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int TotalCapacity { get; set; }


    /// <summary>Integer ID dari konfigurasi AI pipeline (mapping area_id int ↔ Guid)</summary>
    public int ExternalId { get; set; }

    public ICollection<ParkingSpot> Spots { get; set; } = new List<ParkingSpot>();
    public ICollection<Camera> Cameras { get; set; } = new List<Camera>();
}