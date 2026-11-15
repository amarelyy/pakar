namespace Pakar.Api.Realtime;

// Payload dari AI pipeline (Python). JSON camelCase -> otomatis ter-bind oleh System.Text.Json.
public record SpotUpdateDto(
    int SpotId,
    string SpotNumber,
    int AreaId,
    int CameraId,
    string Status,          // "occupied" | "available"
    double Confidence,
    DateTimeOffset Timestamp);

public record IngestRequest(
    int CameraId,
    int AreaId,
    string Type,            // "change" | "heartbeat"
    DateTimeOffset Timestamp,
    List<SpotUpdateDto> Spots);

// Yang dikirim ke frontend
public record SpotView(int SpotId, string SpotNumber, int AreaId, string Status, double Confidence, DateTimeOffset UpdatedAt);
public record ZoneView(int AreaId, int Capacity, int Occupied, int Available, string Status); // Status: available | almost_full | full
public record CameraView(int CameraId, int AreaId, bool Active, DateTimeOffset LastSeen);
public record ParkingSnapshot(List<SpotView> Spots, List<ZoneView> Zones, List<CameraView> Cameras);
