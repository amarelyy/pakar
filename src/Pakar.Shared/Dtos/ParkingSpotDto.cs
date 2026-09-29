// TODO: Define Lat/Long & Status
namespace Pakar.Shared.Dtos;

public class ParkingSpotDto
{
    public int SpotId { get; set; }
    public string DetectedStatus { get; set; } = string.Empty; // "available" | "occupied"
    public double ConfidenceScore { get; set; }
}
