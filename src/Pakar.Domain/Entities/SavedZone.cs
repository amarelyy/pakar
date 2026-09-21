namespace Pakar.Domain.Entities;

public class SavedZone
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;

    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    // Relasi ke Zone
    public Guid ZoneId { get; set; }
    public ParkingZone Zone { get; set; } = null!;
}