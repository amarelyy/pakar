namespace Pakar.Application.DTOs;

public class ZoneDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TotalCapacity { get; set; }
    public int AvailableSpots { get; set; }
    public List<SpotDto> Spots { get; set; } = new();
}

// Kita gabungkan SpotDto di file yang sama atau file terpisah, pastikan namespace-nya sama
public class SpotDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool IsOccupied { get; set; }
}