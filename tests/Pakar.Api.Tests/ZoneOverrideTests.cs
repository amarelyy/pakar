using Pakar.Application.DTOs;
using Pakar.Application.Validation;
using Pakar.Domain.Services;

namespace Pakar.Api.Tests;

public class ZoneOverrideTests
{
    [Fact]
    public void WithoutAnOverrideTheComputedStatusIsUsed()
    {
        var (status, available, overridden) = OccupancyCalculator.Resolve(capacity: 10, occupied: 4, manual: null);
        Assert.Equal(ZoneOccupancyStatus.Available, status);
        Assert.Equal(6, available);
        Assert.False(overridden);
    }

    [Theory]
    [InlineData(ZoneOccupancyStatus.Full, 0)]
    [InlineData(ZoneOccupancyStatus.AlmostFull, 6)]
    [InlineData(ZoneOccupancyStatus.Available, 6)]
    public void AnOverrideReplacesTheStatusAndAFullZoneOffersNoSpots(ZoneOccupancyStatus manual, int expectedAvailable)
    {
        var (status, available, overridden) = OccupancyCalculator.Resolve(capacity: 10, occupied: 4, manual);
        Assert.Equal(manual, status);
        Assert.Equal(expectedAvailable, available);
        Assert.True(overridden);
    }

    [Theory]
    [InlineData("full", ZoneOccupancyStatus.Full)]
    [InlineData("almost_full", ZoneOccupancyStatus.AlmostFull)]
    [InlineData("  Available ", ZoneOccupancyStatus.Available)]
    public void ValidRequestsAreParsed(string input, ZoneOccupancyStatus expected)
    {
        var (status, note, errors) = ZoneOverrideRules.Validate(new SetZoneOverrideRequest(input, "  Camera 2 is offline  "));
        Assert.Empty(errors);
        Assert.Equal(expected, status);
        Assert.Equal("Camera 2 is offline", note);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("busy")]
    public void MissingOrUnknownStatusIsRejected(string? input)
    {
        var (status, _, errors) = ZoneOverrideRules.Validate(new SetZoneOverrideRequest(input, null));
        Assert.Null(status);
        Assert.Contains(nameof(SetZoneOverrideRequest.Status), errors.Keys);
    }

    [Fact]
    public void ABlankNoteBecomesNullAndALongOneIsRejected()
    {
        Assert.Null(ZoneOverrideRules.Validate(new SetZoneOverrideRequest("full", "   ")).Note);
        var tooLong = new string('x', ZoneOverrideRules.MaxNoteLength + 1);
        Assert.Contains(nameof(SetZoneOverrideRequest.Note), ZoneOverrideRules.Validate(new SetZoneOverrideRequest("full", tooLong)).Errors.Keys);
    }
}
