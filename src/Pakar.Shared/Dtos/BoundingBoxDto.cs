namespace Pakar.Shared.Dtos;

/// <summary>
/// One detected object in a camera frame. Coordinates are normalized to 0..1 relative to the
/// frame size, so clients can draw SVG overlays at any display resolution.
/// </summary>
public sealed record BoundingBoxDto(
    float X,
    float Y,
    float Width,
    float Height,
    float Confidence,
    string Label);
