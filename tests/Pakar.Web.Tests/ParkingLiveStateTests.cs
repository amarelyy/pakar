using Pakar.Application.DTOs;
using Pakar.Web.Services;

namespace Pakar.Web.Tests;

public sealed class ParkingLiveStateTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-15T10:00:00Z");
    private static ZoneDto Zone(int externalId = 42) => new()
    {
        Id = Guid.NewGuid(), ExternalId = externalId, Name = "Same name", TotalCapacity = 2, AvailableSpots = 1,
        Spots = [new() { Id = Guid.NewGuid(), ExternalId = externalId * 10, IsOccupied = true, LastUpdated = Now.AddMinutes(-1).UtcDateTime },
                 new() { Id = Guid.NewGuid(), ExternalId = externalId * 10 + 1, LastUpdated = Now.AddMinutes(-1).UtcDateTime }]
    };
    [Fact] public void DeltaMapsByExternalIdsDespiteDuplicateNames()
    {
        var a = Zone(); var b = Zone(99); var state = new ParkingLiveState(); state.Reset([a,b]);
        state.Apply(new LiveSpot(991,"Same name",99,"occupied",.9,Now));
        Assert.Equal(1,a.AvailableSpots); Assert.Equal(0,b.AvailableSpots); Assert.Equal("full",b.Status);
    }
    [Fact] public void PartialSnapshotNeverReplacesPersistedOccupancyWithIncompleteHubCount()
    {
        var zone=Zone();var state=new ParkingLiveState();state.Reset([zone]);
        state.Apply(new LiveSnapshot([new(421,"Slot",42,"occupied",.9,Now)],[new(42,2,1,1,"available")],[]));
        Assert.Equal(0,zone.AvailableSpots);Assert.Equal("full",zone.Status);
    }
    [Fact] public void OlderSnapshotCannotUndoNewerPersistedSpot()
    {
        var zone=Zone();var state=new ParkingLiveState();state.Reset([zone]);
        state.Apply(new LiveSpot(420,"Slot",42,"available",.9,Now.AddHours(-1)));
        Assert.True(zone.Spots[0].IsOccupied);
    }
    [Fact] public void SwapWithSameTotalStillUpdatesBothSpots()
    {
        var zone=Zone();var state=new ParkingLiveState();state.Reset([zone]);
        state.Apply(new LiveSpot(420,"Slot",42,"available",.9,Now));
        state.Apply(new LiveSpot(421,"Slot",42,"occupied",.9,Now));
        Assert.False(zone.Spots[0].IsOccupied);Assert.True(zone.Spots[1].IsOccupied);Assert.Equal(1,zone.AvailableSpots);
    }
    [Fact] public void SourceAgesOutWithoutAnotherHubEvent()
    {
        var zone=Zone();var state=new ParkingLiveState();state.Reset([zone]);
        state.Apply(new LiveCamera(12,42,true,Now));
        Assert.False(ParkingLiveState.IsStale(zone,Now));
        Assert.True(ParkingLiveState.IsStale(zone,Now.AddSeconds(121)));
    }
    [Fact] public void UnknownExternalIdDoesNotChangeAnotherZone()
    {
        var zone=Zone();var state=new ParkingLiveState();state.Reset([zone]);
        state.Apply(new LiveSpot(420,"Slot",99,"available",.9,Now));Assert.True(zone.Spots[0].IsOccupied);
    }
    [Fact] public void CurrentUnknownOverridesFixtureAndAgesOutWithoutNewEvents()
    {
        var zone=Zone();zone.UsesObservations=true;var state=new ParkingLiveState();state.Reset([zone]);
        var now=DateTimeOffset.UtcNow;
        state.Apply(new Pakar.Shared.Dtos.SourceObservationDto(1,42,now,0,false,[new(420,"A","available",null,.9),new(421,"B",null,"no_matching_detection",0)]));
        Assert.Equal(1,zone.AvailableSpots);Assert.Equal(1,zone.UnknownSpots);Assert.Equal("unknown",zone.Status);
        state.Apply(new LiveZone(42,2,0,2,"available"));Assert.Equal(1,zone.AvailableSpots);
        state.Refresh(now.AddSeconds(16));Assert.Equal(0,zone.AvailableSpots);Assert.Equal(2,zone.UnknownSpots);
    }
    [Fact] public void OlderCameraUpdateCannotUndoReactivation()
    {
        var zone=Zone();var state=new ParkingLiveState();state.Reset([zone]);
        state.Apply(new LiveCamera(12,42,true,Now));state.Apply(new LiveCamera(12,42,false,Now.AddMinutes(-1)));
        Assert.False(ParkingLiveState.IsStale(zone,Now));
    }
}
