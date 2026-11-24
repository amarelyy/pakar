using Pakar.Application.DTOs;
using Pakar.Domain.Services;

namespace Pakar.Web.Services;

public sealed record ParkingSpotUpdate(int SpotId, int AreaId, string Status, DateTimeOffset UpdatedAt);
public sealed record ParkingZoneUpdate(int AreaId, string Status, bool IsOverridden);
public sealed record ParkingSourceUpdate(int CameraId, int AreaId, bool Active, DateTimeOffset LastSeen);
public sealed record ParkingHubSnapshot(
    List<ParkingSpotUpdate> Spots, List<ParkingZoneUpdate> Zones, List<ParkingSourceUpdate> Cameras);

/// <summary>Applies hub changes to the complete API zone list. The hub may know only a subset of slots after a restart.</summary>
public sealed class ParkingMapState
{
    private readonly Dictionary<int, ParkingSourceUpdate> sources = new();
    private readonly Dictionary<int, ZoneDto> zonesByArea = new();

    public IReadOnlyList<ZoneDto> Zones { get; private set; } = [];
    public bool CanApplyLiveUpdates { get; private set; }

    public void Reset(IReadOnlyList<ZoneDto> zones)
    {
        Zones = zones;
        CanApplyLiveUpdates = zones.All(zone => zone.ExternalId.HasValue &&
            zone.Spots.All(spot => spot.ExternalId.HasValue));

        sources.Clear();
        zonesByArea.Clear();
        foreach (var zone in zones)
        {
            if (zone.ExternalId is not int areaId)
                continue;

            zonesByArea.Add(areaId, zone);
            foreach (var source in zone.Sources)
            {
                if (source.LastActiveTime is DateTime lastActive)
                {
                    sources[source.ExternalId] = new ParkingSourceUpdate(
                        source.ExternalId, areaId, true, AsUtc(lastActive));
                }
            }
        }
    }

    public void Apply(ParkingHubSnapshot snapshot)
    {
        foreach (var update in snapshot.Spots)
            Apply(update);
        foreach (var update in snapshot.Zones)
            Apply(update);
        foreach (var update in snapshot.Cameras)
            Apply(update);
    }

    public void Apply(ParkingSpotUpdate update)
    {
        if (!zonesByArea.TryGetValue(update.AreaId, out var zone))
            return;

        var spot = zone.Spots.FirstOrDefault(spot => spot.ExternalId == update.SpotId);
        if (spot is null || update.UpdatedAt < AsUtc(spot.LastUpdated))
            return;

        spot.IsOccupied = update.Status == "occupied";
        spot.LastUpdated = update.UpdatedAt.UtcDateTime;
        Recount(zone);
    }

    public void Apply(ParkingZoneUpdate update)
    {
        if (!zonesByArea.TryGetValue(update.AreaId, out var zone))
            return;

        zone.IsOverridden = update.IsOverridden;
        if (update.IsOverridden)
            zone.Status = update.Status;

        Recount(zone);
    }

    public void Apply(ParkingSourceUpdate update)
    {
        if (!sources.TryGetValue(update.CameraId, out var previous) || update.LastSeen >= previous.LastSeen)
            sources[update.CameraId] = update;
    }

    public bool IsWaitingForSource(ZoneDto zone) =>
        zone.ExternalId is int areaId && !sources.Values.Any(source => source.AreaId == areaId);

    public bool IsStale(ZoneDto zone, DateTimeOffset now)
    {
        if (zone.ExternalId is not int areaId)
            return false;

        var current = sources.Values.Where(source => source.AreaId == areaId).ToList();
        return current.Count == 0 ||
            zone.Sources.Any(source => !sources.ContainsKey(source.ExternalId)) ||
            current.Any(source => !source.Active || now - source.LastSeen > TimeSpan.FromMinutes(2));
    }

    private static DateTimeOffset AsUtc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static void Recount(ZoneDto zone)
    {
        var occupied = zone.Spots.Count(spot => spot.IsOccupied);
        var manualStatus = zone.IsOverridden && OccupancyCalculator.TryParseApiValue(zone.Status, out var parsed)
            ? (ZoneOccupancyStatus?)parsed
            : null;
        var result = OccupancyCalculator.Resolve(zone.TotalCapacity, occupied, manualStatus);
        zone.AvailableSpots = result.Available;
        zone.Status = result.Status.ToApiValue();
    }
}
