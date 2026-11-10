namespace Pakar.Application.DTOs;

/// <summary>Public zone listing with its spots (FR 1, FR 3).</summary>
public class ZoneDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TotalCapacity { get; set; }
    public int AvailableSpots { get; set; }

    /// <summary>available, almost_full or full.</summary>
    public string Status { get; set; } = "available";

    public List<SpotDto> Spots { get; set; } = new();
}

public class SpotDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool IsOccupied { get; set; }
}
