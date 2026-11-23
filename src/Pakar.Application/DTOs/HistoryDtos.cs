namespace Pakar.Application.DTOs;

/// <summary>Zone occupancy over time. Bucket is "hour" or "day"; times are UTC.</summary>
public sealed record ZoneHistoryQuery(DateTimeOffset? From, DateTimeOffset? To, string? Bucket);

/// <summary>
/// One time bucket of detection history. OccupancyRate is the share of observations that said "occupied"
/// (0 to 1); the AI sends a state for every slot at every change and heartbeat, so it follows the real occupancy.
/// </summary>
public sealed record ZoneHistoryPointDto(
    DateTime Time,
    int Observations,
    int OccupiedObservations,
    double OccupancyRate,
    int SpotsObserved);

public sealed record ZoneHistoryDto(
    Guid ZoneId,
    string ZoneName,
    string Bucket,
    DateTime From,
    DateTime To,
    IReadOnlyList<ZoneHistoryPointDto> Points);

/// <summary>Filters for the admin list of raw detection records. All filters are optional.</summary>
public sealed record DetectionHistoryQuery(
    Guid? ZoneId,
    Guid? CameraId,
    int? SpotExternalId,
    string? Status,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int? Page,
    int? PageSize);

public sealed record DetectionRecordDto(
    Guid Id,
    DateTime Timestamp,
    string Status,
    float Confidence,
    int SpotExternalId,
    Guid? SpotId,
    string? SpotName,
    Guid? ZoneId,
    Guid CameraId,
    string CameraName);
