namespace Pakar.Domain.Entities;

/// <summary>
/// The region of a video source's frame that belongs to one parking spot, drawn by hand by an admin.
/// A detection box whose centre falls inside the polygon marks the spot as occupied.
/// </summary>
public class SpotRoi
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CameraId { get; set; }
    public Camera Camera { get; set; } = null!;

    public Guid SpotId { get; set; }
    public ParkingSpot Spot { get; set; } = null!;

    /// <summary>
    /// Polygon corners as JSON, <c>[[x, y], ...]</c>, with x and y between 0 and 1 (fractions of the frame width and
    /// height) so the shape does not depend on the video resolution.
    /// </summary>
    public string PointsJson { get; set; } = "[]";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
