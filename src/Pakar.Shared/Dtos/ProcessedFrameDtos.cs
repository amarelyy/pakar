namespace Pakar.Shared.Dtos;

public sealed record ObservedSlotDto(int SpotId, string SpotNumber, string? Status, string? Reason, double Confidence);
public sealed record SourceObservationDto(int CameraId, int AreaId, DateTimeOffset ObservedAt, double MediaSeconds, bool Looped, List<ObservedSlotDto> Slots);
public sealed record ProcessedFrameDto(SourceObservationDto Observation, string VideoFile, byte[] Jpeg, List<WorkerSlotDto> Polygons, List<BoundingBoxDto> Boxes);
