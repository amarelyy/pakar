using System.Collections.Concurrent;
using Pakar.Shared.Dtos;
using Pakar.Domain.Services;

namespace Pakar.Application.DTOs;

/// <summary>Fresh evidence is separate from persisted last-known occupancy. Empty memory after restart is unknown.</summary>
public sealed class CurrentObservations
{
    private readonly ConcurrentDictionary<int, SourceObservationDto> sources = new();
    private readonly object gate=new();
    public bool Apply(SourceObservationDto value)
    {
        lock(gate)
        {
            if(sources.TryGetValue(value.CameraId,out var old) && value.ObservedAt<=old.ObservedAt)return false;
            sources[value.CameraId]=value;return true;
        }
    }
    public SourceObservationDto[] Snapshot() => sources.Values.ToArray();
    public string? Status(int spotId, DateTimeOffset now)
    {
        var readings = sources.Values.Where(s => now-s.ObservedAt <= TimeSpan.FromSeconds(15) && s.ObservedAt <= now.AddSeconds(5))
            .SelectMany(s => s.Slots.Where(p=>p.SpotId==spotId).Select(p=>(p.Status,s.ObservedAt))).OrderByDescending(p=>p.ObservedAt).ToList();
        return readings.FirstOrDefault().Status;
    }
    public static string ZoneStatus(int capacity, int occupied, int available) =>
        occupied+available < capacity ? "unknown" : Pakar.Domain.Services.OccupancyCalculator.GetStatus(capacity, occupied).ToApiValue();
}
