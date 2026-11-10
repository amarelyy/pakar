using Pakar.Domain.Services;

namespace Pakar.Api.Tests;

public class OccupancyCalculatorTests
{
    [Theory]
    [InlineData(10, 0, ZoneOccupancyStatus.Available)]
    [InlineData(10, 8, ZoneOccupancyStatus.Available)]
    [InlineData(10, 9, ZoneOccupancyStatus.AlmostFull)]
    [InlineData(10, 10, ZoneOccupancyStatus.Full)]
    [InlineData(10, 12, ZoneOccupancyStatus.Full)]
    [InlineData(0, 0, ZoneOccupancyStatus.Available)]
    public void Status_follows_occupancy_ratio(int capacity, int occupied, ZoneOccupancyStatus expected)
    {
        Assert.Equal(expected, OccupancyCalculator.GetStatus(capacity, occupied));
    }

    [Theory]
    [InlineData(10, 8, 10)]
    [InlineData(10, 12, 12)]
    [InlineData(-1, 3, 3)]
    public void Capacity_is_never_below_mapped_spots(int configured, int spots, int expected)
    {
        Assert.Equal(expected, OccupancyCalculator.EffectiveCapacity(configured, spots));
    }

    [Fact]
    public void Available_is_never_negative()
    {
        Assert.Equal(0, OccupancyCalculator.Available(5, 7));
        Assert.Equal(3, OccupancyCalculator.Available(5, 2));
    }

    [Theory]
    [InlineData(ZoneOccupancyStatus.Available, "available")]
    [InlineData(ZoneOccupancyStatus.AlmostFull, "almost_full")]
    [InlineData(ZoneOccupancyStatus.Full, "full")]
    public void Api_values_round_trip(ZoneOccupancyStatus status, string apiValue)
    {
        Assert.Equal(apiValue, status.ToApiValue());
        Assert.True(OccupancyCalculator.TryParseApiValue(apiValue, out var parsed));
        Assert.Equal(status, parsed);
    }

    [Theory]
    [InlineData("ALMOST_FULL", ZoneOccupancyStatus.AlmostFull)]
    [InlineData("AlmostFull", ZoneOccupancyStatus.AlmostFull)]
    [InlineData(" full ", ZoneOccupancyStatus.Full)]
    public void Parsing_ignores_case_and_underscores(string value, ZoneOccupancyStatus expected)
    {
        Assert.True(OccupancyCalculator.TryParseApiValue(value, out var parsed));
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [InlineData("busy")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("3")]
    public void Unknown_values_are_rejected(string? value)
    {
        Assert.False(OccupancyCalculator.TryParseApiValue(value, out _));
    }
}
