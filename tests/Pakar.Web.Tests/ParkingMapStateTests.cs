using Pakar.Application.DTOs;
using Pakar.Web.Services;

namespace Pakar.Web.Tests;

public class ParkingMapStateTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PartialHubSnapshotKeepsOtherPersistedSpotsAndIgnoresOlderUpdates()
    {
        var zone = MakeZone();
        var state = new ParkingMapState();
        state.Reset([zone]);

        state.Apply(new ParkingHubSnapshot(
            [
                new ParkingSpotUpdate(1, 10, "available", Now.AddSeconds(-1)),
                new ParkingSpotUpdate(2, 10, "occupied", Now.AddSeconds(1))
            ],
            [new ParkingZoneUpdate(10, "available", false)],
            []));

        Assert.Equal(3, zone.TotalCapacity);
        Assert.Equal(1, zone.AvailableSpots);
        Assert.True(zone.Spots[0].IsOccupied);
        Assert.True(zone.Spots[1].IsOccupied);
    }

    [Fact]
    public void ManualFullStaysEffectiveDuringSpotChangesUntilCleared()
    {
        var zone = MakeZone();
        var state = new ParkingMapState();
        state.Reset([zone]);

        state.Apply(new ParkingZoneUpdate(10, "full", true));
        state.Apply(new ParkingSpotUpdate(1, 10, "available", Now.AddSeconds(1)));

        Assert.True(zone.IsOverridden);
        Assert.Equal("full", zone.Status);
        Assert.Equal(0, zone.AvailableSpots);

        state.Apply(new ParkingZoneUpdate(10, "full", false));

        Assert.False(zone.IsOverridden);
        Assert.Equal("available", zone.Status);
        Assert.Equal(3, zone.AvailableSpots);
    }

    [Fact]
    public void SourceActivityUsesFreshestHeartbeatAndExpiresWithoutAnEvent()
    {
        var zone = MakeZone();
        zone.Sources.Add(new ZoneSourceDto(7, Now.AddMinutes(-3).UtcDateTime));
        var state = new ParkingMapState();
        state.Reset([zone]);
        Assert.True(state.IsStale(zone, Now));

        state.Apply(new ParkingSourceUpdate(7, 10, true, Now));
        state.Apply(new ParkingSourceUpdate(7, 10, false, Now.AddSeconds(-1)));

        Assert.False(state.IsStale(zone, Now));
        Assert.True(state.IsStale(zone, Now.AddMinutes(3)));
    }

    [Fact]
    public void MissingExternalIdsRequireTheRestFallback()
    {
        var zone = MakeZone();
        zone.Spots[0].ExternalId = null;
        var state = new ParkingMapState();

        state.Reset([zone]);

        Assert.False(state.CanApplyLiveUpdates);
    }

    private static ZoneDto MakeZone() => new()
    {
        ExternalId = 10,
        AvailableSpots = 2,
        TotalCapacity = 3,
        Spots =
        [
            new SpotDto { ExternalId = 1, IsOccupied = true, LastUpdated = Now.UtcDateTime },
            new SpotDto { ExternalId = 2, LastUpdated = Now.UtcDateTime },
            new SpotDto { ExternalId = 3, LastUpdated = Now.UtcDateTime }
        ]
    };
}
