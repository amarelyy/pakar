using Pakar.Application.DTOs;
using Pakar.Shared.Dtos;

namespace Pakar.Api.Tests;

public sealed class CurrentObservationTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-15T12:00:00Z");
    private static SourceObservationDto Reading(DateTimeOffset at, string? status) => new(1,1,at,12,false,[new(9,"S9",status,status is null?"no_matching_detection":null,.9)]);
    [Fact] public void FreshEmptyIsEvidenceButMissingAndExpiredAreNotEmpty()
    {
        var state=new CurrentObservations(); Assert.Null(state.Status(9,Now));
        state.Apply(Reading(Now,"available")); Assert.Equal("available",state.Status(9,Now));
        Assert.Null(state.Status(9,Now.AddSeconds(16)));
        state.Apply(Reading(Now.AddSeconds(1),null)); Assert.Null(state.Status(9,Now.AddSeconds(1)));
    }
    [Fact] public void LateReadingCannotUndoNewerUnknown()
    {
        var state=new CurrentObservations();state.Apply(Reading(Now,null));state.Apply(Reading(Now.AddSeconds(-1),"available"));Assert.Null(state.Status(9,Now));
    }
    [Theory] [InlineData(3,0,0,"unknown")] [InlineData(3,1,1,"unknown")] [InlineData(3,3,0,"full")] [InlineData(3,0,3,"available")]
    public void PartialCoverageDoesNotImplyAvailableOrFull(int capacity,int occupied,int available,string expected) => Assert.Equal(expected,CurrentObservations.ZoneStatus(capacity,occupied,available));
}
