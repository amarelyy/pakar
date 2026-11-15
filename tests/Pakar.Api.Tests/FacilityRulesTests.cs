using Pakar.Domain.Services;

namespace Pakar.Api.Tests;

public class FacilityRulesTests
{
    [Fact]
    public void Valid_zone_has_no_errors()
    {
        Assert.Empty(FacilityRules.ValidateZone("Building A", "Front area", 10, existingSpotCount: 0));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Zone_name_is_required(string? name)
    {
        var errors = FacilityRules.ValidateZone(name, null, 10, 0);

        Assert.True(errors.ContainsKey("Name"));
    }

    [Fact]
    public void Zone_name_has_a_maximum_length()
    {
        var errors = FacilityRules.ValidateZone(new string('x', FacilityRules.MaxZoneNameLength + 1), null, 10, 0);

        Assert.True(errors.ContainsKey("Name"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(FacilityRules.MaxZoneCapacity + 1)]
    public void Zone_capacity_must_be_in_range(int capacity)
    {
        var errors = FacilityRules.ValidateZone("Building A", null, capacity, 0);

        Assert.True(errors.ContainsKey("TotalCapacity"));
    }

    [Fact]
    public void Zone_capacity_cannot_drop_below_existing_spots()
    {
        var errors = FacilityRules.ValidateZone("Building A", null, totalCapacity: 8, existingSpotCount: 10);

        Assert.Contains("10 spots", errors["TotalCapacity"][0]);
    }

    [Fact]
    public void Zone_capacity_equal_to_spot_count_is_allowed()
    {
        Assert.Empty(FacilityRules.ValidateZone("Building A", null, totalCapacity: 10, existingSpotCount: 10));
    }

    [Fact]
    public void Zone_description_has_a_maximum_length()
    {
        var errors = FacilityRules.ValidateZone("Building A", new string('x', FacilityRules.MaxDescriptionLength + 1), 10, 0);

        Assert.True(errors.ContainsKey("Description"));
    }

    [Fact]
    public void Camera_name_is_required()
    {
        Assert.True(FacilityRules.ValidateCamera(" ", "lot-a.mp4").ContainsKey("Name"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Video_file_is_optional(string? file)
    {
        Assert.Null(FacilityRules.ValidateVideoFile(file));
        Assert.Empty(FacilityRules.ValidateCamera("Cam", file));
    }

    [Theory]
    [InlineData("lot-a.mp4")]
    [InlineData("Lot A.MOV")]
    [InlineData("demo.mkv")]
    [InlineData("demo.webm")]
    public void Video_file_accepts_plain_video_names(string file)
    {
        Assert.Null(FacilityRules.ValidateVideoFile(file));
    }

    [Theory]
    [InlineData("../secret.mp4")]
    [InlineData("folder/lot-a.mp4")]
    [InlineData("folder\\lot-a.mp4")]
    [InlineData("C:lot-a.mp4")]
    [InlineData("..lot.mp4")]
    public void Video_file_cannot_point_outside_the_video_folder(string file)
    {
        Assert.NotNull(FacilityRules.ValidateVideoFile(file));
    }

    [Theory]
    [InlineData("lot-a.exe")]
    [InlineData("lot-a")]
    [InlineData("lot-a.mp4.txt")]
    public void Video_file_must_have_a_video_extension(string file)
    {
        Assert.NotNull(FacilityRules.ValidateVideoFile(file));
    }

    [Fact]
    public void Video_file_has_a_maximum_length()
    {
        var file = new string('a', FacilityRules.MaxVideoFileLength) + ".mp4";

        Assert.NotNull(FacilityRules.ValidateVideoFile(file));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("  lot-a.mp4 ", "lot-a.mp4")]
    public void Video_file_is_trimmed_for_storage(string? input, string expected)
    {
        Assert.Equal(expected, FacilityRules.NormalizeVideoFile(input));
    }
}
