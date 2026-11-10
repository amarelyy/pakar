namespace Pakar.Application.DTOs;

/// <summary>
/// FR 2 filters. All are optional: Q matches zone name or description (for example a building),
/// Status is available, almost_full or full, and Lat with Lng sorts results by distance.
/// </summary>
public sealed record ZoneSearchQuery(
    string? Q = null,
    string? Status = null,
    int? MinAvailable = null,
    double? Lat = null,
    double? Lng = null,
    int? Limit = null);

/// <summary>Zone availability without the spot list. Latitude and Longitude are the spots' center.</summary>
public sealed record ZoneSummaryDto(
    Guid Id,
    string Name,
    string Description,
    int TotalCapacity,
    int OccupiedSpots,
    int AvailableSpots,
    string Status,
    double? Latitude,
    double? Longitude,
    double? DistanceMeters);

/// <summary>
/// FR 4: the nearest zone with free spots. Alternative is null when no other zone has space.
/// IsFull tells the client whether a suggestion is needed for the requested zone.
/// </summary>
public sealed record AlternateZoneDto(
    Guid ZoneId,
    string ZoneName,
    string ZoneStatus,
    bool IsFull,
    ZoneSummaryDto? Alternative,
    string Message);
