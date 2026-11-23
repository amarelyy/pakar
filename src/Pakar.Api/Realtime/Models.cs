using Pakar.Application.DTOs;

namespace Pakar.Api.Realtime;

// Payload dari AI pipeline (Python). JSON camelCase -> otomatis ter-bind oleh System.Text.Json.
public record SpotUpdateDto(
    int SpotId,
    string SpotNumber,
    int AreaId,
    int CameraId,
    string Status,          // "occupied" | "available" | "unknown"
    double Confidence,
    DateTimeOffset Timestamp);

public record IngestRequest(
    int CameraId,
    int AreaId,
    string Type,            // "change" | "heartbeat"
    DateTimeOffset Timestamp,
    List<SpotUpdateDto> Spots);

// Yang dikirim ke frontend
public record ZoneView(int AreaId, int Capacity, int Occupied, int Available, string Status, bool IsOverridden = false); // Status: available | almost_full | full; IsOverridden = set by an admin (FR 17)
public record CameraView(int CameraId, int AreaId, bool Active, DateTimeOffset LastSeen);
public record ParkingSnapshot(List<SpotView> Spots, List<ZoneView> Zones, List<CameraView> Cameras);
