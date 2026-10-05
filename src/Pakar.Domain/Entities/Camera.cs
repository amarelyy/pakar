namespace Pakar.Domain.Entities;

public class Camera
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string StreamUrl { get; set; } = string.Empty; 
    
    public string ConnectionType { get; set; } = "IP"; 
    public string ApiKeyHash { get; set; } = string.Empty; 

    /// <summary>Integer ID dari konfigurasi AI pipeline / ROI config (untuk mapping int↔Guid)</summary>
    public int ExternalId { get; set; }

    /// <summary>Waktu terakhir kamera mengirim heartbeat / deteksi (FR12 watchdog)</summary>
    public DateTimeOffset? LastActiveTime { get; set; }

    public Guid ZoneId { get; set; }
    public ParkingZone Zone { get; set; } = null!;
}