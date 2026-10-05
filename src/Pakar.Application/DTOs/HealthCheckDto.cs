namespace Pakar.Application.DTOs;

public class HealthCheckDto
{
    public string Message { get; set; } = string.Empty;
    public int TotalZones { get; set; }
}