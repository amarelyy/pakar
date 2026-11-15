using Pakar.Shared.Dtos;

namespace Pakar.Application.DTOs;

/// <summary>Publishes pixels and metadata together; late concurrent requests cannot rewind either.</summary>
public sealed class ProcessedFrameStore(CurrentObservations observations)
{
    private readonly object gate=new();
    private readonly Dictionary<int,ProcessedFrameDto> frames=new();
    public ProcessedFrameDto? Get(int cameraId) { lock(gate) return frames.GetValueOrDefault(cameraId); }
    public bool TryPublish(ProcessedFrameDto frame)
    {
        lock(gate)
        {
            var o=frame.Observation;
            if(frames.TryGetValue(o.CameraId,out var old) && old.Observation.ObservedAt>=o.ObservedAt) return false;
            if(!observations.Apply(o)) return false;
            frames[o.CameraId]=frame;return true;
        }
    }
    public SourceObservationDto? Invalidate(int cameraId, DateTimeOffset now)
    {
        lock(gate)
        {
            var old=observations.Snapshot().FirstOrDefault(o=>o.CameraId==cameraId);
            frames.Remove(cameraId);
            if(old is null)return null;
            var at=now>old.ObservedAt ? now : old.ObservedAt.AddTicks(1);
            var invalid=old with {ObservedAt=at,Slots=old.Slots.Select(s=>s with {Status=null,Reason="configuration_changed",Confidence=0}).ToList()};
            observations.Apply(invalid);return invalid;
        }
    }
}
