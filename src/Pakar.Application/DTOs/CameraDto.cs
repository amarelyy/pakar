using System.ComponentModel.DataAnnotations;

namespace Pakar.Application.DTOs;

public class CameraDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string IpAddress { get; set; } = string.Empty;

    [Required]
    public Guid ZoneId { get; set; }
}