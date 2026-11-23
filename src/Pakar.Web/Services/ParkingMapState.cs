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

    public void Reset(IReadOnlyList<ZoneDto> zones, DateTimeOffset now)
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

        Refresh(now);
    }

    public void Apply(ParkingHubSnapshot snapshot, DateTimeOffset now)
    {
        foreach (var update in snapshot.Spots)
            ApplyReading(update);
        foreach (var update in snapshot.Zones)
            Apply(update, now);
        foreach (var update in snapshot.Cameras)
            Apply(update);
        Refresh(now);
    }

    public void Apply(IReadOnlyList<ParkingSpotUpdate> updates, DateTimeOffset now)
    {
        foreach (var update in updates)
            ApplyReading(update);
        Refresh(now);
    }

    private void ApplyReading(ParkingSpotUpdate update)
    {
        if (!zonesByArea.TryGetValue(update.AreaId, out var zone))
            return;

        var spot = zone.Spots.FirstOrDefault(spot => spot.ExternalId == update.SpotId);
        if (spot is null || (spot.ObservedAt is { } observed && update.UpdatedAt < observed))
            return;

        spot.ObservedAt = update.UpdatedAt;
        spot.ObservedStatus = update.Status;
    }

    public void Apply(ParkingZoneUpdate update, DateTimeOffset now)
    {
        if (!zonesByArea.TryGetValue(update.AreaId, out var zone))
            return;

        zone.IsOverridden = update.IsOverridden;
        if (update.IsOverridden)
            zone.Status = update.Status;

        Recount(zone, now);
    }

    public void Apply(ParkingSourceUpdate update)
    {
        if (!sources.TryGetValue(update.CameraId, out var previous) || update.LastSeen >= previous.LastSeen)
            sources[update.CameraId] = update;
    }

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

    public bool Refresh(DateTimeOffset now)
    {
        var changed = false;
        foreach (var zone in Zones)
        {
            var previous = (zone.AvailableSpots, zone.Status);
            Recount(zone, now);
            changed |= previous != (zone.AvailableSpots, zone.Status);
        }

        return changed;
    }

    public static string SpotLabel(string status) => status switch
    {
        "occupied" => "Terisi",
        "available" => "Kosong",
        _ => "Belum diketahui"
    };

    private static void Recount(ZoneDto zone, DateTimeOffset now)
    {
        foreach (var spot in zone.Spots)
            spot.ObservedStatus = ObservedOccupancy.StatusAt(spot.ObservedStatus, spot.ObservedAt, now);

        var occupied = zone.Spots.Count(spot => spot.ObservedStatus == "occupied");
        var available = zone.Spots.Count(spot => spot.ObservedStatus == "available");
        var manualStatus = zone.IsOverridden && OccupancyCalculator.TryParseApiValue(zone.Status, out var parsed)
            ? (ZoneOccupancyStatus?)parsed
            : null;
        var result = ObservedOccupancy.Resolve(zone.TotalCapacity, occupied, available, manualStatus);
        zone.OccupiedSpots = occupied;
        zone.AvailableSpots = result.Available;
        zone.UnknownSpots = result.Unknown;
        zone.Status = result.Status;
    }
}
