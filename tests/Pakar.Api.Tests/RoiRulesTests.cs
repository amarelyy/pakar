using Pakar.Application.Validation;
using Pakar.Shared.Dtos;

namespace Pakar.Api.Tests;

public class RoiRulesTests
{
    private readonly Guid spot = Guid.NewGuid();
    private static double[][] Square() => [[.1, .1], [.4, .1], [.4, .4], [.1, .4]];

    [Fact]
    public void EmptyListClearsAndNullIsInvalid()
    {
        Assert.Empty(RoiRules.Validate(new([]), new HashSet<Guid>()));
        Assert.NotEmpty(RoiRules.Validate(new(null), new HashSet<Guid>()));
    }
    [Fact]
    public void ValidSlotMustBelongToZoneAndBeUnique()
    {
        var slot = new RoiSlotRequest(spot, Square());
        Assert.Empty(RoiRules.Validate(new([slot]), new HashSet<Guid> { spot }));
        Assert.NotEmpty(RoiRules.Validate(new([slot]), new HashSet<Guid>()));
        Assert.NotEmpty(RoiRules.Validate(new([slot, slot]), new HashSet<Guid> { spot }));
    }
    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void RejectsInvalidCoordinate(double value)
    {
        var points = Square(); points[0][0] = value;
        Assert.NotEmpty(RoiRules.Validate(new([new(spot, points)]), new HashSet<Guid> { spot }));
    }
    [Fact]
    public void RejectsMalformedTooManyTinyAndCrossingPoints()
    {
        double[][][] invalid = [[[0,0],[1,1]],Enumerable.Range(0,51).Select(i=>new[]{i/100d,i/100d}).ToArray(),
            [[0,0],[1,1],[0,1],[1,0]],[[.1,.1],[.1001,.1],[.1001,.1001]],[[0],[1,1],[0,1]]];
        foreach (var points in invalid) Assert.NotEmpty(RoiRules.Validate(new([new(spot, points)]), new HashSet<Guid> { spot }));
    }
    [Fact]
    public void SignatureOverridesExtensionAndRejectsOversize()
    {
        Assert.Equal("image/png", ReferenceFrameRules.ContentType([137, 80, 78, 71, 13, 10, 26, 10]));
        Assert.Equal("image/jpeg", ReferenceFrameRules.ContentType([255, 216, 255, 224]));
        Assert.Null(ReferenceFrameRules.ContentType("not a png"u8));
        Assert.Null(ReferenceFrameRules.ContentType(new byte[ReferenceFrameRules.MaxBytes + 1]));
    }
}
