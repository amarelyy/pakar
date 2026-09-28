namespace Pakar.Application.DTOs;

public class ZoneResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TotalCapacity { get; set; }
}