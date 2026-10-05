namespace Pakar.Domain.Entities;

public class Camera
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string StreamUrl { get; set; } = string.Empty; 
    
    // Properties required by Program.cs
    public string ConnectionType { get; set; } = "IP"; 
    public string ApiKeyHash { get; set; } = string.Empty; 

    public Guid ZoneId { get; set; }
    public ParkingZone Zone { get; set; } = null!;
}