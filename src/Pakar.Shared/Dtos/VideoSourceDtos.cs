namespace Pakar.Shared.Dtos;

public sealed record WorkerSlotDto(int SpotId, string SpotNumber, double[][] Polygon);
public sealed record WorkerSourceDto(int CameraId, int AreaId, string Name, string VideoFile,
    List<WorkerSlotDto> Slots, int ZoneCapacity);
public sealed record RoiSlotRequest(Guid SpotId, double[][]? Points);
public sealed record RoiRequest(List<RoiSlotRequest>? Slots);
public sealed record RoiSlotDto(Guid SpotId, string SpotName, int SpotExternalId,
    double[][] Points, DateTime? UpdatedAt);
// Spots includes unconfigured spots too, so the editor can create the first ROI.
public sealed record CameraRoisDto(Guid CameraId, int CameraExternalId, Guid ZoneId,
    List<RoiSlotDto> Slots, List<RoiSpotDto> Spots);
public sealed record RoiSpotDto(Guid Id, string Name, int ExternalId);
