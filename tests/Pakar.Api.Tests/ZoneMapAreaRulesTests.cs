using Pakar.Application.DTOs;
using Pakar.Application.Validation;

namespace Pakar.Api.Tests;

public class ZoneMapAreaRulesTests
{
    private static ZoneMapPoint[] Corners() =>
    [
        new(-7.76610, 110.37310), new(-7.76610, 110.37318),
        new(-7.76615, 110.37318), new(-7.76615, 110.37310)
    ];

    [Fact]
    public void AcceptsParkingSizedBoundaryAndAnExplicitClear()
    {
        Assert.Null(ZoneMapAreaRules.Validate(new(Corners(), "DTSL")));
        Assert.Null(ZoneMapAreaRules.Validate(new([])));
    }

    [Fact]
    public void RejectsCrossingEdgesAndPointsOutsideCampus()
    {
        var corners = Corners();
        Assert.NotNull(ZoneMapAreaRules.Validate(new([corners[0], corners[2], corners[1], corners[3]])));
        Assert.NotNull(ZoneMapAreaRules.Validate(new([new(0, 0), .. corners[1..]])));
    }
}
