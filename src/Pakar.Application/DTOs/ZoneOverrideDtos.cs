namespace Pakar.Application.DTOs;

/// <summary>Admin request to force a zone's displayed status (FR 17). Status is available, almost_full or full.</summary>
public sealed record SetZoneOverrideRequest(string? Status, string? Note);

/// <summary>An active manual status. ZoneExternalId is the area id used by the realtime hub.</summary>
public sealed record ZoneOverrideDto(
    Guid ZoneId,
    string ZoneName,
    int ZoneExternalId,
    string Status,
    string? Note,
    DateTime SetAt,
    string? SetBy);

/// <summary>The zone an override was cleared from, so the caller can refresh its realtime state.</summary>
public sealed record ZoneRefDto(Guid ZoneId, string ZoneName, int ZoneExternalId);
