namespace Pakar.Domain.Entities;

public sealed class CameraFrame
{
    public Guid CameraId { get; set; }
    public Camera Camera { get; set; } = null!;
    public string ContentType { get; set; } = string.Empty;
    public byte[] Data { get; set; } = [];
    public DateTime UpdatedAt { get; set; }
}
