namespace Pakar.Domain.Entities;

public class Camera
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string ConnectionType { get; set; } = string.Empty; // e.g., "Android-Wireless"
    public string ApiKeyHash { get; set; } = string.Empty; // Untuk validasi di Middleware
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    // Foreign Keys
    public Guid ZoneId { get; set; }
    public ParkingZone Zone { get; set; } = null!;

    // Navigation
    public ICollection<Detection> Detections { get; set; } = new List<Detection>();
}