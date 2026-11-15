using Pakar.Application.DTOs;
using Pakar.Domain.Services;
using Pakar.Shared.Dtos;

namespace Pakar.Web.Services;

public sealed record LiveSpot(int SpotId, string SpotNumber, int AreaId, string Status, double Confidence, DateTimeOffset UpdatedAt);
public sealed record LiveZone(int AreaId, int Capacity, int Occupied, int Available, string Status, bool IsOverridden = false);
public sealed record LiveCamera(int CameraId, int AreaId, bool Active, DateTimeOffset LastSeen);
public sealed record LiveSnapshot(List<LiveSpot> Spots, List<LiveZone> Zones, List<LiveCamera> Cameras);

/// <summary>Merges in-memory hub deltas into the complete persisted REST baseline by ExternalId.</summary>
public sealed class ParkingLiveState
{
    public List<ZoneDto> Zones { get; private set; } = [];
    private CurrentObservations observations = new();
    public void Apply(SourceObservationDto value) { observations.Apply(value); Refresh(DateTimeOffset.UtcNow); }
    public void Refresh(DateTimeOffset now) { foreach(var zone in Zones.Where(z=>z.UsesObservations)) { foreach(var spot in zone.Spots) spot.ObservedStatus=observations.Status(spot.ExternalId,now); Recount(zone); } }
    public void Reset(List<ZoneDto> zones) { observations=new(); Zones=zones; }
    public void Apply(LiveSnapshot snapshot)
    {
        foreach (var spot in snapshot.Spots) Apply(spot);
        foreach (var zone in snapshot.Zones) Apply(zone);
        foreach (var camera in snapshot.Cameras) Apply(camera);
    }
    public void Apply(LiveSpot update)
    {
        var zone = Zones.FirstOrDefault(z => z.ExternalId == update.AreaId);
        var spot = zone?.Spots.FirstOrDefault(s => s.ExternalId == update.SpotId);
        if (spot is null || update.UpdatedAt.UtcDateTime < DateTime.SpecifyKind(spot.LastUpdated, DateTimeKind.Utc)) return;
        spot.IsOccupied = update.Status == "occupied";
        spot.LastUpdated = update.UpdatedAt.UtcDateTime;
        Recount(zone!);
    }
    public void Apply(LiveZone update)
    {
        var zone = Zones.FirstOrDefault(z => z.ExternalId == update.AreaId);
        if (zone is null) return;
        zone.TotalCapacity = Math.Max(update.Capacity, zone.Spots.Count);
        zone.IsOverridden = update.IsOverridden;
        // Hub memory may contain only a subset after API restart. Recount the complete spot list.
        Recount(zone);
        if (update.IsOverridden) zone.Status = update.Status;
    }
    public void Apply(LiveCamera update)
    {
        var zone = Zones.FirstOrDefault(z => z.ExternalId == update.AreaId);
        if (zone is null) return;
        var existing = zone.Sources.FindIndex(c => c.ExternalId == update.CameraId);
        if (existing >= 0 && zone.Sources[existing].LastActiveTime > update.LastSeen.UtcDateTime) return;
        var value = new ZoneSourceDto(update.CameraId, update.Active, update.LastSeen.UtcDateTime);
        if (existing < 0) zone.Sources.Add(value); else zone.Sources[existing] = value;
    }
    private static void Recount(ZoneDto zone)
    {
        var occupied = zone.Spots.Count(s => zone.UsesObservations ? s.ObservedStatus=="occupied" : s.IsOccupied);
        if(zone.UsesObservations) { zone.AvailableSpots=zone.Spots.Count(s=>s.ObservedStatus=="available"); zone.UnknownSpots=Math.Max(0,zone.TotalCapacity-occupied-zone.AvailableSpots); if(!zone.IsOverridden) zone.Status=CurrentObservations.ZoneStatus(zone.TotalCapacity,occupied,zone.AvailableSpots); return; }
        zone.AvailableSpots = OccupancyCalculator.Available(zone.TotalCapacity, occupied);
        if (!zone.IsOverridden) zone.Status = OccupancyCalculator.GetStatus(zone.TotalCapacity, occupied).ToApiValue();
    }
    public static bool IsStale(ZoneDto zone, DateTimeOffset now) => zone.Sources.Count == 0 ||
        zone.Sources.Any(s => !s.IsActive || s.LastActiveTime is null || now.UtcDateTime - DateTime.SpecifyKind(s.LastActiveTime.Value, DateTimeKind.Utc) > (zone.UsesObservations ? TimeSpan.FromSeconds(15) : TimeSpan.FromMinutes(2)));
}
