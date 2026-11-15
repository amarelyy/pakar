namespace Pakar.Application.DTOs;

/// <summary>Body for adding or editing a zone (FR 13).</summary>
public sealed record ZoneRequest(string? Name, string? Description, int TotalCapacity);

/// <summary>Zone as the admin sees it, including the id the AI worker uses (<see cref="ExternalId"/>).</summary>
public sealed record ZoneAdminDto(
    Guid Id,
    string Name,
    string Description,
    int TotalCapacity,
    int ExternalId,
    int SpotCount,
    int CameraCount);

/// <summary>
/// Body for adding or editing a video source (FR 14). <see cref="VideoFile"/> is a file name inside the
/// configured video folder and may be left empty until the video is available.
/// </summary>
public sealed record CameraRequest(string? Name, Guid ZoneId, string? VideoFile);

/// <summary>Video source as the admin sees it. <see cref="ExternalId"/> is the camera id the AI worker sends.</summary>
public sealed record CameraAdminDto(
    Guid Id,
    string Name,
    Guid ZoneId,
    string ZoneName,
    int ZoneExternalId,
    int ExternalId,
    string ConnectionType,
    string VideoFile,
    bool IsActive,
    DateTime? LastActiveTime);
